using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Capture;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis;

internal sealed partial class CaptureMemoryService : ICaptureMemoryService, IDisposable
{
    private readonly CaptureMemoryAuthorization _authorization;
    private readonly ICaptureAssetCatalog _catalog;
    private readonly IAnalysisExecutionStore _store;
    private readonly ICaptureAnalysisWorker _worker;
    private readonly ICaptureMemoryPrompts _prompts;
    private readonly IFileSystem _files;
    private readonly CaptureNamingService? _naming;
    private readonly IReadOnlyList<IMetadataProcessor> _processors;
    private readonly IAnalysisFeatureAvailability? _availability;
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
        CaptureNamingService? naming = null, IEnumerable<IMetadataProcessor>? processors = null,
        IAnalysisFeatureAvailability? availability = null)
    {
        _authorization = authorization;
        _catalog = catalog;
        _store = store;
        _worker = worker;
        _prompts = prompts;
        _files = files;
        _naming = naming;
        _processors = processors?.ToArray() ?? [];
        _availability = availability;
        _worker.ProgressChanged += OnProgress;
    }

    public CaptureMemoryState State => new(
        Policy: _authorization.Policy,
        PolicyAvailable: _authorization.IsLoaded && (!_authorization.Policy.IsAllowed || _authorization.IsAllowed),
        Storage: Volatile.Read(ref _storage),
        Activity: _worker.Progress,
        IsScheduling: false,
        FailureCode: Volatile.Read(ref _failure),
        IsDeleting: Volatile.Read(ref _deleting) > 0,
        ConsentAvailable: _authorization.ConsentAvailable);

    public event Action? StateChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RunSafelyAsync(async ct =>
        {
            await RunCommandAsync(async () =>
            {
                if (_initialized)
                {
                    return;
                }

                await InitializePolicyAsync(ct).ConfigureAwait(false);
                await InitializeStorageAsync(ct).ConfigureAwait(false);
                _initialized = true;
            }, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task RegisterCaptureAsync(CaptureAsset asset, CancellationToken cancellationToken = default)
    {
        return RunSafelyAsync(ct => RunCommandAsync(async () =>
        {
            IReadOnlyList<CaptureAsset> registrations = await _catalog.ReadAllAsync(ct).ConfigureAwait(false);
            CaptureAsset? existing = registrations.FirstOrDefault(entry => SamePath(entry.SourcePath, asset.SourcePath));
            if (existing == null)
            {
                await _catalog.RegisterAsync(asset, ct).ConfigureAwait(false);
            }
        }, ct), cancellationToken, "capture-registration");
    }

    public Task SetPreferredPathAsync(string sourcePath, string preferredPath, CancellationToken cancellationToken = default) =>
        RunSafelyAsync(ct => RunCommandAsync(async () =>
        {
            var asset = (await _catalog.ReadAllAsync(ct).ConfigureAwait(false)).FirstOrDefault(item => SamePath(item.SourcePath, sourcePath));
            if (asset != null)
            {
                await _catalog.SetPreferredPathAsync(asset.Id, preferredPath, ct).ConfigureAwait(false);
            }
        }, ct), cancellationToken, "capture-registration");

    public Task DeleteMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _deleting) != 0)
        {
            return Task.CompletedTask;
        }

        long epoch = Interlocked.Increment(ref _epoch);
        return RunSafelyAsync(ct => OfferDeletionAsync(epoch, ct), cancellationToken);
    }

    private async Task OfferDeletionAsync(long epoch, CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _deleting, 1, 0) != 0)
        {
            return;
        }

        Publish();
        try
        {
            await RunCommandAsync(() => RefreshStatusAsync(ct), ct).ConfigureAwait(false);
            if (!IsCurrentEpoch(epoch) || _storage.HasData != true || !await _prompts.ConfirmAsync(CaptureMemoryPrompt.DeleteMetadata, ct).ConfigureAwait(false))
            {
                return;
            }

            await RunCommandAsync(async () =>
            {
                if (!IsCurrentEpoch(epoch))
                {
                    return;
                }

                await _worker.ClearAsync(ct).ConfigureAwait(false);
                await InitializeStorageAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _deleting, 0);
            Publish();
        }
    }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => RunSafelyAsync(ct => RunCommandAsync(async () =>
    {
        if (!_authorization.IsLoaded)
        {
            await InitializePolicyAsync(ct).ConfigureAwait(false);
        }

        await RefreshStatusAsync(ct).ConfigureAwait(false);
    }, ct), cancellationToken);

    private async Task InitializeStorageAsync(CancellationToken ct)
    {
        try
        {
            await _store.InitializeAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await RefreshStatusAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task ObserveRunnerAsync()
    {
        try
        {
            await Task.Run(() => _worker.RunAsync(_lifetime.Token), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            SetFailure("storage-unavailable");
        }
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
            _storage = _storage with
            {
                IsAvailable = false
            };
            _failure = "storage-unavailable";
        }
        Publish();
    }

    private async Task RunCommandAsync(Func<Task> action, CancellationToken ct)
    {
        await _commands.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _commands.Release();
            Publish();
        }
    }

    private async Task RunSafelyAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken, string failure = "storage-unavailable")
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            await action(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (CaptureMemoryPolicyException exception)
        {
            SetFailure(exception.Code);
        }
        catch (Exception)
        {
            SetFailure(failure);
        }
    }

    private bool IsCurrentEpoch(long epoch) => epoch == Interlocked.Read(ref _epoch);

    // Under the command gate, only an accepted newer grant can supersede a
    // denial. Metadata commands and declined prompts cannot discard its save.
    private bool CanApplyPolicy(long epoch, bool granting) => granting ? IsCurrentEpoch(epoch) : epoch >= _lastGrantedEpoch;

    private static bool SamePath(string? first, string? second) => first != null && second != null &&
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private void SetFailure(string code)
    {
        _failure = code;
        Publish();
    }

    private void Publish()
    {
        if (StateChanged is { } changed)
        {
            foreach (Action observer in changed.GetInvocationList())
            {
                try
                {
                    observer();
                }
                catch (Exception) { }
            }
        }
    }

    public async Task StopAsync()
    {
        _authorization.Block();
        await _lifetime.CancelAsync().ConfigureAwait(false);
        if (_runner != null)
        {
            await _runner.ConfigureAwait(false);
        }

        if (_naming != null)
        {
            await _naming.StopAsync().ConfigureAwait(false);
        }

        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            await _store.DiscardPendingAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _commands.Release();
        }
    }

    public void Dispose()
    {
        _worker.ProgressChanged -= OnProgress;
        _lifetime.Dispose();
        _commands.Dispose();
        _consentRequests.Dispose();
    }
}
