using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture;

/// <summary>Consumes committed titles without owning inference or an editor lifetime.</summary>
internal sealed class CaptureNamingService : ICaptureNamingService, IDisposable
{
    private readonly ICaptureAssetCatalog _catalog;
    private readonly ICaptureNameStore _names;
    private readonly ICaptureMetadataReader _metadata;
    private readonly IAnalysisExecutionStore _execution;
    private readonly IAnalysisSource _sources;
    private readonly CaptureMemoryAuthorization _authorization;
    private readonly ICaptureAnalysisWorker _worker;
    private readonly IRecentCapturesChangeNotifier _recents;
    private readonly IFileSystem _files;
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _workGate = new();
    private Task _runner = Task.CompletedTask;
    private readonly HashSet<CaptureId> _requested = [];
    private bool _reconcileAll;
    private bool _stopped;
    private Status _status = new(null, false);
    private int _blocked;
    private long _version;
    private sealed record Status(Guid? Epoch, bool Available);

    public bool IsEnabled => Volatile.Read(ref _status).Epoch != null;
    public bool IsAvailable => Volatile.Read(ref _status).Available;
    internal Guid? Enrollment => Volatile.Read(ref _blocked) == 0 && IsAvailable && _authorization.IsAllowed ? Volatile.Read(ref _status).Epoch : null;
    public event Action? Changed;

    public CaptureNamingService(ICaptureAssetCatalog catalog, ICaptureNameStore names, ICaptureMetadataReader metadata,
        IAnalysisExecutionStore execution, IAnalysisSource sources, CaptureMemoryAuthorization authorization,
        ICaptureAnalysisWorker worker, IRecentCapturesChangeNotifier recents, IFileSystem files)
    {
        _catalog = catalog; _names = names; _metadata = metadata; _execution = execution; _sources = sources;
        _authorization = authorization; _worker = worker; _recents = recents; _files = files;
        _worker.ResultCommitted += OnResultCommitted;
    }

    internal async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            await _commands.WaitAsync(ct).ConfigureAwait(false);
            try { await ReadStatusAsync(ct).ConfigureAwait(false); }
            finally { _commands.Release(); }
            QueueReconcile();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception) { Unavailable(); }
    }

    public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _stopped)) return false;
        long version = Interlocked.Increment(ref _version);
        if (!enabled) Interlocked.Exchange(ref _blocked, 1);
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (enabled && !_authorization.IsAllowed) return false;
            await _names.SetAutomaticNamingAsync(enabled, cancellationToken).ConfigureAwait(false);
            await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
            if (version == Interlocked.Read(ref _version)) Interlocked.Exchange(ref _blocked, 0);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        catch (Exception) { Unavailable(); return false; }
        finally { _commands.Release(); Publish(); }
    }

    /// <summary>Called before a policy revocation or clear. The catalog epoch fences an in-flight title write.</summary>
    internal async Task InvalidatePendingAsync(CancellationToken ct)
    {
        Interlocked.Exchange(ref _blocked, 1);
        long version = Interlocked.Increment(ref _version);
        await _commands.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _names.InvalidatePendingNamesAsync(ct).ConfigureAwait(false);
            await ReadStatusAsync(ct).ConfigureAwait(false);
            if (version == Interlocked.Read(ref _version)) Interlocked.Exchange(ref _blocked, 0);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception) { Unavailable(); }
        finally { _commands.Release(); }
    }

    public async Task<CaptureName?> GetNameAsync(string path, CancellationToken cancellationToken = default)
    {
        try { return (await ResolveAsync(path, cancellationToken).ConfigureAwait(false))?.Name; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return null; }
        catch (Exception) { return null; } // Naming availability never blocks Save As or basic file details.
    }

    public async Task<bool> SetNameAsync(string path, CaptureFileType mediaType, string name, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _stopped)) return false;
        try
        {
            var chosen = new CaptureName(name, false);
            if (!Path.IsPathFullyQualified(path) || !_files.FileExists(path)) return false;
            await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var matches = await MatchesAsync(path, cancellationToken).ConfigureAwait(false);
                if (matches.Length > 1) return false;
                var asset = matches.SingleOrDefault();
                if (asset == null)
                {
                    asset = new(CaptureId.New(), mediaType, null, path, CaptureSourceOwnership.External);
                    await _catalog.RegisterAsync(asset, cancellationToken).ConfigureAwait(false);
                }
                await _names.SetUserNameAsync(asset.Id, chosen.Text, cancellationToken).ConfigureAwait(false);
            }
            finally { _commands.Release(); }
            NotifyNamesChanged();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        catch (Exception) { return false; }
    }

    private async Task<CaptureAsset?> ResolveAsync(string path, CancellationToken ct)
    {
        var matches = await MatchesAsync(path, ct).ConfigureAwait(false);
        return matches.Length == 1 ? matches[0] : null;
    }
    private async Task<CaptureAsset[]> MatchesAsync(string path, CancellationToken ct) =>
        (await _catalog.ReadAllAsync(ct).ConfigureAwait(false)).Where(asset => SamePath(path, asset.SourcePath) || SamePath(path, asset.PreferredPath)).Take(2).ToArray();
    private static bool SamePath(string path, string? other) => other != null && Path.IsPathFullyQualified(path) &&
        string.Equals(Path.GetFullPath(path), Path.GetFullPath(other), StringComparison.OrdinalIgnoreCase);

    private void OnResultCommitted(CaptureId id, AnalysisCapability capability)
    {
        if (capability == AnalysisCapability.CaptureSynopsis) QueueReconcile(id);
    }
    private void QueueReconcile(CaptureId? id = null)
    {
        lock (_workGate)
        {
            if (_stopped) return;
            if (id is { } capture) _requested.Add(capture);
            else _reconcileAll = true;
            if (_runner.IsCompleted) _runner = Task.Run(DrainAsync);
        }
    }
    private async Task DrainAsync()
    {
        while (true)
        {
            HashSet<CaptureId>? captures;
            lock (_workGate)
            {
                if ((!_reconcileAll && _requested.Count == 0) || _stopped) { _runner = Task.CompletedTask; return; }
                captures = _reconcileAll ? null : [.. _requested];
                _requested.Clear();
                _reconcileAll = false;
            }
            try { await ReconcileAsync(_lifetime.Token, captures).ConfigureAwait(false); }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
            catch (Exception) { Unavailable(); }
        }
    }

    internal async Task ReconcileAsync(CancellationToken ct, IReadOnlySet<CaptureId>? captures = null)
    {
        if (Enrollment is not { } epoch) return;
        var snapshot = await _names.ReadNamingAsync(ct).ConfigureAwait(false);
        if (snapshot.Epoch != epoch) return;
        var scope = await _execution.GetAdmissionScopeAsync(ct).ConfigureAwait(false);
        foreach (var entry in snapshot.Pending.Where(entry => captures == null || captures.Contains(entry.Asset.Id)))
        {
            if (Enrollment != epoch) return;
            // The deletion boundary also survives a failed catalog invalidation or restart.
            if (entry.AutomaticAuthorization != _authorization.Policy.Revision || entry.Sequence <= scope.ReconciliationBoundary) continue;
            try
            {
                var record = await _metadata.GetAsync(entry.Asset.Id, cancellationToken: ct).ConfigureAwait(false);
                var title = record?.Results.Select(result => result.Payload).OfType<CaptureSynopsisMetadata>().SingleOrDefault()?.Title?.Text;
                if (title == null) continue;
                var chosen = new CaptureName(title, true);
                await using var source = await _sources.OpenAsync(entry.Asset.SourcePath, ct).ConfigureAwait(false);
                if (record!.SourceRevision != source.Revision || !await source.VerifyAsync(ct).ConfigureAwait(false)) continue;
                using var authorization = await _authorization.AcquireAsync(ct).ConfigureAwait(false);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, authorization.Revoked);
                if (!authorization.IsAllowed || authorization.Revision != entry.AutomaticAuthorization || Enrollment != epoch ||
                    scope.Generation != (await _execution.GetAdmissionScopeAsync(linked.Token).ConfigureAwait(false)).Generation) continue;
                if (await _names.TryApplyAutomaticNameAsync(entry.Asset.Id, chosen.Text, epoch, entry.Asset.SourcePath, linked.Token).ConfigureAwait(false)) NotifyNamesChanged();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    private async Task ReadStatusAsync(CancellationToken ct)
    {
        var snapshot = await _names.ReadNamingAsync(ct).ConfigureAwait(false);
        Volatile.Write(ref _status, new(snapshot.Epoch, true));
        Publish();
    }
    private void Unavailable()
    {
        Status status;
        do { status = Volatile.Read(ref _status); }
        while (!ReferenceEquals(Interlocked.CompareExchange(ref _status, status with { Available = false }, status), status));
        Publish();
    }
    private void NotifyNamesChanged() { Publish(); _recents.NotifyRecentCapturesChanged(); }
    private void Publish()
    {
        if (Changed is { } changed)
            foreach (Action observer in changed.GetInvocationList()) try { observer(); } catch (Exception) { }
    }
    internal async Task StopAsync()
    {
        Task runner;
        lock (_workGate) { _stopped = true; runner = _runner; }
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await runner.ConfigureAwait(false);
        await _commands.WaitAsync().ConfigureAwait(false);
        _commands.Release();
    }
    public void Dispose() { _worker.ResultCommitted -= OnResultCommitted; _lifetime.Dispose(); _commands.Dispose(); }
}
