using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis;

internal sealed class CaptureMemoryService : ICaptureMemoryService, IDisposable
{
    private readonly CaptureMemoryAuthorization _authorization;
    private readonly ICaptureAssetCatalog _catalog;
    private readonly IAnalysisExecutionStore _store;
    private readonly ICaptureAnalysisWorker _worker;
    private readonly ICaptureMemoryPrompts _prompts;
    private readonly IRecentCaptureCatalog _recents;
    private readonly IFileSystem _files;
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _runner;
    private bool _initialized;
    private long _epoch;
    private AnalysisStorageStatus _storage = new(null, false);
    private int _scheduling;
    private int _deleting;
    private string? _failure;

    public CaptureMemoryService(CaptureMemoryAuthorization authorization, ICaptureAssetCatalog catalog,
        IAnalysisExecutionStore store, ICaptureAnalysisWorker worker, ICaptureMemoryPrompts prompts, IRecentCaptureCatalog recents, IFileSystem files)
    {
        _authorization = authorization;
        _catalog = catalog;
        _store = store;
        _worker = worker;
        _prompts = prompts;
        _recents = recents;
        _files = files;
        _worker.ProgressChanged += OnProgress;
    }
    public CaptureMemoryState State => new(_authorization.Policy, _authorization.IsLoaded &&
        (!_authorization.Policy.IsAllowed || _authorization.IsAllowed), Volatile.Read(ref _storage), _worker.Progress,
        Volatile.Read(ref _scheduling) > 0, Volatile.Read(ref _failure), Volatile.Read(ref _deleting) > 0);
    public event Action? StateChanged;
    public Guid? CaptureAuthorization => _authorization.IsAllowed ? _authorization.Policy.Revision : null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await GuardAsync(async ct =>
        {
            await LockedAsync(async () =>
            {
                if (_initialized) return;
                await InitializePolicyAsync(ct).ConfigureAwait(false);
                await RecoverAsync(ct).ConfigureAwait(false);
                _initialized = true;
            }, ct).ConfigureAwait(false);
            await ReconcileAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task RegisterCaptureAsync(CaptureAsset asset, Guid? authorization, CancellationToken cancellationToken = default) =>
        GuardAsync(ct => LockedAsync(async () =>
        {
            IReadOnlyList<CaptureRegistration> registrations = await _catalog.ReadRegistrationsAsync(ct).ConfigureAwait(false);
            CaptureRegistration? existing = registrations.FirstOrDefault(entry => SamePath(entry.Asset.SourcePath, asset.SourcePath));
            if (existing == null)
                await _catalog.RegisterForAnalysisAsync(asset, authorization, ct).ConfigureAwait(false);
            CaptureRegistration registration = existing ?? (await _catalog.ReadRegistrationsAsync(ct).ConfigureAwait(false)).Single(entry => entry.Asset.Id == asset.Id);
            await AdmitAutomaticAsync(registration, ct).ConfigureAwait(false);
        }, ct), cancellationToken, "capture-registration");

    public Task SetPreferredPathAsync(string sourcePath, string preferredPath, CancellationToken cancellationToken = default) =>
        GuardAsync(ct => LockedAsync(async () =>
        {
            var asset = (await _catalog.ReadAllAsync(ct).ConfigureAwait(false)).FirstOrDefault(item => SamePath(item.SourcePath, sourcePath));
            if (asset != null) await _catalog.SetPreferredPathAsync(asset.Id, preferredPath, ct).ConfigureAwait(false);
        }, ct), cancellationToken, "capture-registration");

    public Task SetScanningAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (enabled && _authorization.IsAllowed) return Task.CompletedTask;
        long epoch = Interlocked.Increment(ref _epoch);
        if (!enabled) { _authorization.Block(); Publish(); }
        return GuardAsync(async ct =>
        {
            if (!_authorization.IsLoaded) { SetFailure("policy-unavailable"); return; }
            if (enabled && !_authorization.Policy.ConsentGranted && !await _prompts.ConfirmAsync(CaptureMemoryPrompt.Consent, ct).ConfigureAwait(false)) return;
            bool applied = false;
            await LockedAsync(async () =>
            {
                if (!Current(epoch)) return;
                CaptureMemoryPolicy policy = _authorization.Policy;
                long boundary = enabled ? await _catalog.GetBoundaryAsync(ct).ConfigureAwait(false) : policy.EnableBoundary;
                await _authorization.SaveAsync(new(enabled, enabled || policy.ConsentGranted,
                    Guid.NewGuid(), boundary), ct).ConfigureAwait(false);
                applied = true;
                if (enabled) await RecoverAsync(ct).ConfigureAwait(false);
                else await RefreshStatusAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
            if (!applied || !Current(epoch)) return;
            if (enabled)
            {
                if (await _prompts.ConfirmAsync(CaptureMemoryPrompt.ScanExisting, ct).ConfigureAwait(false) && Current(epoch))
                    await ScanAsync(epoch, ct).ConfigureAwait(false);
            }
            else await OfferDeletionAsync(epoch, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task SetConsentAsync(bool granted, CancellationToken cancellationToken = default)
    {
        long epoch = Interlocked.Increment(ref _epoch);
        if (!granted) { _authorization.Block(); Publish(); }
        return GuardAsync(async ct =>
        {
            if (!_authorization.IsLoaded) { SetFailure("policy-unavailable"); return; }
            if (granted && !await _prompts.ConfirmAsync(CaptureMemoryPrompt.Consent, ct).ConfigureAwait(false)) return;
            await LockedAsync(async () =>
            {
                if (!Current(epoch)) return;
                CaptureMemoryPolicy policy = _authorization.Policy;
                await _authorization.SaveAsync(new(granted && _authorization.IsAllowed, granted,
                    granted ? policy.Revision : Guid.NewGuid(), policy.EnableBoundary), ct).ConfigureAwait(false);
                await RefreshStatusAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
            if (!granted && Current(epoch)) await OfferDeletionAsync(epoch, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task ScanExistingAsync(CancellationToken cancellationToken = default)
    {
        long epoch = Interlocked.Increment(ref _epoch);
        return GuardAsync(ct => ScanAsync(epoch, ct), cancellationToken);
    }

    private async Task ScanAsync(long epoch, CancellationToken ct)
    {
        if (!_authorization.IsAllowed || !Current(epoch)) return;
        Guid authorization = _authorization.Policy.Revision;
        AnalysisAdmissionScope? scope = null;
        IReadOnlyList<CaptureAsset> assets = [];
        Interlocked.Increment(ref _scheduling);
        Publish();
        try
        {
            await LockedAsync(async () =>
            {
                if (!Current(epoch) || !_authorization.IsAllowed) return;
                await RecoverAsync(ct).ConfigureAwait(false);
                scope = await _store.GetAdmissionScopeAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
            if (scope == null) return;
            await MigrateRecentsAsync(epoch, ct).ConfigureAwait(false);
            assets = await _catalog.ReadAllAsync(ct).ConfigureAwait(false);
            foreach (CaptureAsset asset in assets)
            {
                await LockedAsync(async () =>
                {
                    if (!Current(epoch) || CaptureAuthorization != authorization || !_files.FileExists(asset.SourcePath)) return;
                    AnalysisWorkItem? prior = await _store.GetWorkAsync(asset.Id, ct).ConfigureAwait(false);
                    await AdmitAsync(asset, scope.Generation, authorization, prior?.Token.RunId, ct).ConfigureAwait(false);
                }, ct).ConfigureAwait(false);
                if (!Current(epoch)) break;
            }
        }
        finally { Interlocked.Decrement(ref _scheduling); Publish(); }
    }

    public Task DeleteMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _deleting) != 0) return Task.CompletedTask;
        long epoch = Interlocked.Increment(ref _epoch);
        return GuardAsync(ct => OfferDeletionAsync(epoch, ct), cancellationToken);
    }
    private async Task OfferDeletionAsync(long epoch, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _deleting, 1, 0) != 0) return;
        Publish();
        try
        {
            await LockedAsync(() => RefreshStatusAsync(ct), ct).ConfigureAwait(false);
            if (!Current(epoch) || _storage.HasData != true || !await _prompts.ConfirmAsync(CaptureMemoryPrompt.DeleteMetadata, ct).ConfigureAwait(false)) return;
            await LockedAsync(async () =>
            {
                if (!Current(epoch)) return;
                long boundary = await _catalog.GetBoundaryAsync(ct).ConfigureAwait(false);
                await _worker.ClearAsync(boundary, ct).ConfigureAwait(false);
                await RecoverAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        finally { Interlocked.Exchange(ref _deleting, 0); Publish(); }
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => GuardAsync(ct => LockedAsync(async () =>
    {
        if (!_authorization.IsLoaded) await InitializePolicyAsync(ct).ConfigureAwait(false);
        await RefreshStatusAsync(ct).ConfigureAwait(false);
    }, ct), cancellationToken);

    private async Task InitializePolicyAsync(CancellationToken ct)
    {
        try { await _authorization.InitializeAsync(ct).ConfigureAwait(false); }
        catch (CaptureMemoryPolicyException)
        {
            // Consent failure blocks scanning, but does not hide the user's ability to delete data.
            _storage = await _store.GetStorageStatusAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        IReadOnlyList<CaptureRegistration> registrations = await _catalog.ReadRegistrationsAsync(ct).ConfigureAwait(false);
        foreach (CaptureRegistration entry in registrations)
            await LockedAsync(() => AdmitAutomaticAsync(entry, ct), ct).ConfigureAwait(false);
    }
    private async Task AdmitAutomaticAsync(CaptureRegistration entry, CancellationToken ct)
    {
        if (!_authorization.IsAllowed) return;
        CaptureMemoryPolicy policy = _authorization.Policy;
        AnalysisAdmissionScope scope = await _store.GetAdmissionScopeAsync(ct).ConfigureAwait(false);
        if (entry.AutomaticAuthorization != policy.Revision || entry.Sequence <= Math.Max(policy.EnableBoundary, scope.ReconciliationBoundary) ||
            !_files.FileExists(entry.Asset.SourcePath) || await _store.GetWorkAsync(entry.Asset.Id, ct).ConfigureAwait(false) != null) return;
        await AdmitAsync(entry.Asset, scope.Generation, policy.Revision, null, ct).ConfigureAwait(false);
    }
    private async Task AdmitAsync(CaptureAsset asset, Guid generation, Guid authorization, Guid? prior, CancellationToken ct)
    {
        AnalysisMediaKind media = asset.MediaType switch
        {
            CaptureFileType.Image => AnalysisMediaKind.Image,
            CaptureFileType.Audio => AnalysisMediaKind.Audio,
            CaptureFileType.Video => AnalysisMediaKind.Video,
            _ => throw new InvalidOperationException("Unsupported capture media.")
        };
        if (await _worker.EnqueueAsync(new(asset.Id, media, asset.SourcePath, Guid.NewGuid(), generation, prior,
            ExpectedAuthorizationId: authorization), ct).ConfigureAwait(false))
        {
            _storage = _storage with { HasData = true };
            Publish();
        }
    }
    private async Task MigrateRecentsAsync(long epoch, CancellationToken ct)
    {
        foreach (RecentCaptureCatalogEntry entry in _recents.GetEntries())
        {
            if (!Current(epoch)) break;
            if (entry.Origin != RecentCaptureOrigin.Captured || entry.CaptureFileType is not (CaptureFileType.Image or CaptureFileType.Audio or CaptureFileType.Video) ||
                !Path.IsPathFullyQualified(entry.FilePath) || !_files.FileExists(entry.FilePath)) continue;
            await LockedAsync(async () =>
            {
                if (!Current(epoch)) return;
                var assets = await _catalog.ReadAllAsync(ct).ConfigureAwait(false);
                if (assets.Any(asset => SamePath(asset.SourcePath, entry.FilePath) || SamePath(asset.PreferredPath, entry.FilePath))) return;
                var asset = new CaptureAsset(CaptureId.New(), entry.CaptureFileType, new DateTimeOffset(DateTime.SpecifyKind(entry.LastActivityUtc, DateTimeKind.Utc)),
                    entry.FilePath, CaptureSourceOwnership.External);
                await _catalog.RegisterAsync(asset, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
    }
    private async Task RecoverAsync(CancellationToken ct)
    {
        await _store.InitializeAsync(ct).ConfigureAwait(false);
        if (_runner != null && _worker.Progress.Activity == AnalysisActivity.StorageUnavailable) await _runner.ConfigureAwait(false);
        await RefreshStatusAsync(ct).ConfigureAwait(false);
        if (_runner == null || _runner.IsCompleted)
            _runner = ObserveRunnerAsync();
    }
    private async Task ObserveRunnerAsync()
    {
        try { await Task.Run(() => _worker.RunAsync(_lifetime.Token), CancellationToken.None).ConfigureAwait(false); }
        catch (Exception) { SetFailure("storage-unavailable"); }
    }
    private async Task RefreshStatusAsync(CancellationToken ct)
    {
        _storage = await _store.GetStorageStatusAsync(ct).ConfigureAwait(false);
        _failure = !_storage.IsAvailable ? "storage-unavailable" : _storage.CleanupPending ? "cleanup-pending" : null;
        Publish();
    }
    private void OnProgress(AnalysisActivitySnapshot progress)
    {
        if (progress.Activity == AnalysisActivity.StorageUnavailable)
        {
            _storage = _storage with { IsAvailable = false };
            _failure = "storage-unavailable";
        }
        Publish();
    }
    private async Task LockedAsync(Func<Task> action, CancellationToken ct)
    {
        await _commands.WaitAsync(ct).ConfigureAwait(false);
        try { await action().ConfigureAwait(false); }
        finally { _commands.Release(); Publish(); }
    }
    private async Task GuardAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken, string failure = "storage-unavailable")
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try { await action(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (CaptureMemoryPolicyException exception) { SetFailure(exception.Code); }
        catch (Exception) { SetFailure(failure); }
    }
    private bool Current(long epoch) => epoch == Interlocked.Read(ref _epoch);
    private static bool SamePath(string? first, string? second) => first != null && second != null &&
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    private void SetFailure(string code) { _failure = code; Publish(); }
    private void Publish()
    {
        if (StateChanged is { } changed)
            foreach (Action observer in changed.GetInvocationList())
                try { observer(); } catch (Exception) { }
    }
    public async Task StopAsync()
    {
        _authorization.Block();
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (_runner != null) await _runner.ConfigureAwait(false);
        await _commands.WaitAsync().ConfigureAwait(false);
        _commands.Release();
    }
    public void Dispose()
    {
        _worker.ProgressChanged -= OnProgress;
        _lifetime.Dispose();
        _commands.Dispose();
    }
}
