using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Capture;
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
    private readonly IFileSystem _files;
    private readonly CaptureNamingService? _naming;
    private readonly IReadOnlyList<IMetadataProcessor> _processors;
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly SemaphoreSlim _consentRequests = new(1, 1);
    private long _consentAttempt;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _runner;
    private bool _initialized;
    private long _epoch;
    private long _lastGrantedEpoch;
    private AnalysisStorageStatus _storage = new(null, false);
    private int _deleting;
    private string? _failure;

    public CaptureMemoryService(CaptureMemoryAuthorization authorization, ICaptureAssetCatalog catalog,
        IAnalysisExecutionStore store, ICaptureAnalysisWorker worker, ICaptureMemoryPrompts prompts, IFileSystem files,
        CaptureNamingService? naming = null, IEnumerable<IMetadataProcessor>? processors = null)
    {
        _authorization = authorization;
        _catalog = catalog;
        _store = store;
        _worker = worker;
        _prompts = prompts;
        _files = files;
        _naming = naming;
        _processors = processors?.ToArray() ?? [];
        _worker.ProgressChanged += OnProgress;
    }
    public CaptureMemoryState State => new(_authorization.Policy, _authorization.IsLoaded &&
        (!_authorization.Policy.IsAllowed || _authorization.IsAllowed), Volatile.Read(ref _storage), _worker.Progress,
        false, Volatile.Read(ref _failure), Volatile.Read(ref _deleting) > 0, _authorization.ConsentAvailable);
    public event Action? StateChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await GuardAsync(async ct =>
        {
            await LockedAsync(async () =>
            {
                if (_initialized) return;
                await InitializePolicyAsync(ct).ConfigureAwait(false);
                await InitializeStorageAsync(ct).ConfigureAwait(false);
                _initialized = true;
            }, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task RegisterCaptureAsync(CaptureAsset asset, CancellationToken cancellationToken = default)
    {
        return GuardAsync(ct => LockedAsync(async () =>
        {
            IReadOnlyList<CaptureRegistration> registrations = await _catalog.ReadRegistrationsAsync(ct).ConfigureAwait(false);
            CaptureRegistration? existing = registrations.FirstOrDefault(entry => SamePath(entry.Asset.SourcePath, asset.SourcePath));
            if (existing == null)
                await _catalog.RegisterAsync(asset, ct).ConfigureAwait(false);
        }, ct), cancellationToken, "capture-registration");
    }

    public Task SetPreferredPathAsync(string sourcePath, string preferredPath, CancellationToken cancellationToken = default) =>
        GuardAsync(ct => LockedAsync(async () =>
        {
            var asset = (await _catalog.ReadAllAsync(ct).ConfigureAwait(false)).FirstOrDefault(item => SamePath(item.SourcePath, sourcePath));
            if (asset != null) await _catalog.SetPreferredPathAsync(asset.Id, preferredPath, ct).ConfigureAwait(false);
        }, ct), cancellationToken, "capture-registration");

    public Task SetConsentAsync(bool granted, CancellationToken cancellationToken = default)
        => granted ? GrantConsentAsync(cancellationToken) : UpdateConsentAsync(false, cancellationToken);

    private async Task GrantConsentAsync(CancellationToken ct)
    {
        long attempt = Interlocked.Read(ref _consentAttempt);
        await _consentRequests.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_authorization.IsConsentAllowed || attempt != Interlocked.Read(ref _consentAttempt)) return;
            await UpdateConsentAsync(true, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _consentAttempt);
        }
        finally { _consentRequests.Release(); }
    }

    private Task UpdateConsentAsync(bool granted, CancellationToken cancellationToken)
    {
        if (granted && _authorization.IsConsentAllowed) return Task.CompletedTask;
        long epoch = Interlocked.Increment(ref _epoch);
        if (!granted) { _authorization.Block(revokeConsent: true); Publish(); }
        return GuardAsync(async ct =>
        {
            if (!_authorization.IsLoaded) { SetFailure("policy-unavailable"); return; }
            if (granted && !await _prompts.ConfirmAsync(CaptureMemoryPrompt.Consent, ct).ConfigureAwait(false)) return;
            bool applied = false;
            await LockedAsync(async () =>
            {
                if (!CanApplyPolicy(epoch, granted)) return;
                CaptureMemoryPolicy policy = _authorization.Policy;
                if (!granted && _naming != null) await _naming.InvalidatePendingAsync(ct).ConfigureAwait(false);
                if (!CanApplyPolicy(epoch, granted)) return;
                // Keep legacy preferences readable, but consent never enables automatic work.
                await _authorization.SaveAsync(new(false, granted, Guid.NewGuid(), policy.EnableBoundary,
                    policy.ScanningPreference), ct, grantConsent: granted).ConfigureAwait(false);
                if (granted) _lastGrantedEpoch = epoch;
                applied = true;
                await RefreshStatusAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
            if (!applied || !Current(epoch)) return;
            if (!granted) await OfferDeletionAsync(epoch, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public async Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (!_authorization.IsConsentAllowed) await SetConsentAsync(true, cancellationToken).ConfigureAwait(false);
        return !cancellationToken.IsCancellationRequested && _authorization.IsConsentAllowed;
    }

    public Task AnalyzeAsync(string path, AnalysisCapability capability, CancellationToken cancellationToken = default)
        => RequestAnalysisAsync(path, capability, scanText: false, cancellationToken);

    public Task ScanTextAsync(string path, CancellationToken cancellationToken = default)
        => RequestAnalysisAsync(path, AnalysisCapability.TextRecognition, scanText: true, cancellationToken);

    private Task RequestAnalysisAsync(string path, AnalysisCapability capability, bool scanText, CancellationToken cancellationToken)
    {
        long epoch = Interlocked.Read(ref _epoch);
        return GuardAsync(ct => LockedAsync(async () =>
        {
            // Admit only the requested output and its prerequisites. The worker reuses
            // cached results and briefly retains the model for subsequent requests.
            if (!Current(epoch) || !_authorization.IsAllowed || Volatile.Read(ref _deleting) != 0 ||
                !Path.IsPathFullyQualified(path) || !_files.FileExists(path)) return;
            CaptureFileType media = CaptureFileTypeDetector.DetectFileType(path);
            if (media is not (CaptureFileType.Image or CaptureFileType.Audio or CaptureFileType.Video)) return;
            if (scanText && media == CaptureFileType.Audio) return;
            // Check the requested semantic output before scheduling expensive prerequisites.
            // Probes cannot download/load models or inspect the capture.
            IMetadataProcessor[] candidates = _processors.Where(processor => processor.Descriptor.Capability == capability).ToArray();
            if (candidates.Length != 0)
            {
                bool available = false;
                foreach (var candidate in candidates)
                {
                    AnalyzerAvailability readiness;
                    try { readiness = await candidate.GetAvailabilityAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false); }
                    catch (Exception) when (!ct.IsCancellationRequested) { continue; }
                    if (readiness is AnalyzerAvailability.Ready or AnalyzerAvailability.PreparationRequired) { available = true; break; }
                }
                if (!available) { SetFailure("model-unavailable"); return; }
            }
            var assets = await _catalog.ReadAllAsync(ct).ConfigureAwait(false);
            CaptureAsset? asset = assets.FirstOrDefault(item => SamePath(item.SourcePath, path)) ??
                assets.FirstOrDefault(item => SamePath(item.PreferredPath, path));
            AnalysisWorkItem? prior = asset == null ? null : await _store.GetWorkAsync(asset.Id, ct).ConfigureAwait(false);
            if (prior?.Run.IsPending == true) return;
            if (!Current(epoch) || !_authorization.IsAllowed) return;
            if (asset == null)
            {
                asset = new(CaptureId.New(), media, null, path, CaptureSourceOwnership.External);
                await _catalog.RegisterAsync(asset, ct).ConfigureAwait(false);
            }
            AnalysisAdmissionScope scope = await _store.GetAdmissionScopeAsync(ct).ConfigureAwait(false);
            if (!Current(epoch) || !_authorization.IsAllowed) return;
            await AdmitAsync(asset, scope.Generation, _authorization.Policy.Revision, prior?.Run.Id, capability, scanText, ct).ConfigureAwait(false);
            await InitializeStorageAsync(ct).ConfigureAwait(false);
        }, ct), cancellationToken);
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
                if (_naming != null) await _naming.InvalidatePendingAsync(ct).ConfigureAwait(false);
                await _worker.ClearAsync(boundary, ct).ConfigureAwait(false);
                await InitializeStorageAsync(ct).ConfigureAwait(false);
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

    private async Task AdmitAsync(CaptureAsset asset, Guid generation, Guid authorization, Guid? prior, AnalysisCapability capability, bool scanText, CancellationToken ct)
    {
        if (_runner != null && _worker.Progress.Activity == AnalysisActivity.StorageUnavailable) await _runner.ConfigureAwait(false);
        AnalysisMediaKind media = asset.MediaType switch
        {
            CaptureFileType.Image => AnalysisMediaKind.Image,
            CaptureFileType.Audio => AnalysisMediaKind.Audio,
            CaptureFileType.Video => AnalysisMediaKind.Video,
            _ => throw new InvalidOperationException("Unsupported capture media.")
        };
        if (await _worker.EnqueueAsync(new(asset.Id, media, asset.SourcePath, Guid.NewGuid(), generation, prior,
            ExpectedAuthorizationId: authorization, Capabilities: scanText
                ? [AnalysisCapability.QrCodeDetection, AnalysisCapability.TextRecognition]
                : CaptureAnalysisConfiguration.ForAction(media, capability), ReuseExisting: true), ct).ConfigureAwait(false))
        {
            _storage = _storage with { HasData = true };
            Publish();
            if (_runner == null || _runner.IsCompleted) _runner = ObserveRunnerAsync();
        }
    }
    private async Task InitializeStorageAsync(CancellationToken ct)
    {
        await _store.InitializeAsync(ct).ConfigureAwait(false);
        await RefreshStatusAsync(ct).ConfigureAwait(false);
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
    // Under the command gate, only an accepted newer grant can supersede a
    // denial. Metadata commands and declined prompts cannot discard its save.
    private bool CanApplyPolicy(long epoch, bool granting) => granting ? Current(epoch) : epoch >= _lastGrantedEpoch;
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
        if (_naming != null) await _naming.StopAsync().ConfigureAwait(false);
        await _commands.WaitAsync().ConfigureAwait(false);
        try { await _store.DiscardPendingAsync(CancellationToken.None).ConfigureAwait(false); }
        finally { _commands.Release(); }
    }
    public void Dispose()
    {
        _worker.ProgressChanged -= OnProgress;
        _lifetime.Dispose();
        _commands.Dispose();
        _consentRequests.Dispose();
    }
}
