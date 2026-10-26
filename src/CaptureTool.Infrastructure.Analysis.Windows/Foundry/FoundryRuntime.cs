using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal sealed partial class FoundryRuntime(IStorageService storage,
    CaptureTool.Application.Abstractions.Ai.IAiDeviceSupport? device = null,
    ModelDownloadGuard? downloads = null) : IAnalysisResources, IDisposable
{
    private ModelDownloadGuard Downloads => downloads ?? ModelDownloadGuard.Current;
    public bool IsSupported => (device ?? CaptureTool.Infrastructure.Ai.WindowsAiDeviceSupport.Current).FoundryCpu;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _modelGate = new(1, 1);
    private IModel? _loadedModel;
    private bool _modelReady;
    private FoundryLocalManager? _manager;
    private bool _ownsManager;
    private bool _retainWhenIdle;
    private readonly Lazy<WindowsMemoryPressure> _memoryPressure = new(() => new());

    public TimeSpan IdleRetention => _modelReady && _retainWhenIdle ? TimeSpan.FromSeconds(30) : TimeSpan.Zero;
    public bool IsUnderMemoryPressure => _memoryPressure.Value.IsLow;

    // One resident model, shared by all adapters. The lease spans native inference,
    // including an invocation that has outlived its worker deadline.
    internal async Task<IAsyncDisposable> AcquireModelAsync(IModel model, CancellationToken ct, bool retainWhenIdle = false)
    {
        await _modelGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_modelReady || _loadedModel?.Id != model.Id)
            {
                await UnloadAsync().ConfigureAwait(false);
                _loadedModel = model;
                try
                {
                    await model.LoadAsync(ct).ConfigureAwait(false);
                    _modelReady = true;
                }
                catch
                {
                    await UnloadAsync().ConfigureAwait(false);
                    throw;
                }
            }
            ct.ThrowIfCancellationRequested();
            _retainWhenIdle = retainWhenIdle;
            return new ModelLease(this, ct);
        }
        catch
        {
            _modelGate.Release();
            throw;
        }
    }

    public async Task ReleaseAsync()
    {
        await _modelGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await UnloadAsync().ConfigureAwait(false);
        }
        finally
        {
            _modelGate.Release();
        }
    }

    private async Task UnloadAsync()
    {
        if (_loadedModel == null)
        {
            return;
        }

        _modelReady = false;
        _retainWhenIdle = false;
        // Keep ownership if unloading fails; never load a second model on top of it.
        await _loadedModel.UnloadAsync(CancellationToken.None).ConfigureAwait(false);
        _loadedModel = null;
    }

    private sealed class ModelLease(FoundryRuntime owner, CancellationToken ct) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (ct.IsCancellationRequested)
                {
                    await owner.UnloadAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                owner._modelGate.Release();
            }
        }
    }

    // Only preparation may initialize the SDK or access its catalog. Passive probes never call here.
    public async Task<IModel?> ResolveAsync(string alias, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return null;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string root = Path.Combine(storage.GetApplicationDataFolderPath(), "AnalysisModels", "FoundryLocal");
            string cache = Path.Combine(root, "models");
            if (_manager == null && !FoundryLocalManager.IsInitialized && Downloads.IsOffline &&
                (!Directory.Exists(cache) || !Directory.EnumerateFileSystemEntries(cache).Any()))
            {
                // A fresh offline installation has nothing to resolve. Do not bootstrap
                // the SDK/catalog or wait through a model download deadline.
                throw new ModelDownloadRequiredException();
            }

            using var resolution = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (Downloads.IsOffline)
            {
                resolution.CancelAfter(TimeSpan.FromSeconds(10));
            }
            CancellationToken resolveToken = resolution.Token;
            if (_manager == null)
            {
                if (!FoundryLocalManager.IsInitialized)
                {
                    await FoundryLocalManager.CreateAsync(new Configuration
                    {
                        AppName = "capture-tool",
                        AppDataDir = root,
                        ModelCacheDir = cache,
                        LogsDir = Path.Combine(root, "logs"),
                        LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Fatal,
                        Web = new Configuration.WebService { Urls = "http://127.0.0.1:0" },
                    }, NullLogger.Instance, resolveToken).ConfigureAwait(false);
                    _ownsManager = true;
                }
                _manager = FoundryLocalManager.Instance;
            }
            ICatalog catalog = await _manager.GetCatalogAsync(resolveToken).ConfigureAwait(false);
            IModel? cached = (await catalog.GetCachedModelsAsync(resolveToken).ConfigureAwait(false))
                .FirstOrDefault(model => model.Alias == alias && model.Info.Runtime?.DeviceType == DeviceType.CPU);
            if (cached != null)
            {
                return cached;
            }
            Downloads.ThrowIfOffline();
            IModel? model = await catalog.GetModelAsync(alias, resolveToken).ConfigureAwait(false);
            IModel? cpu = model?.Variants.FirstOrDefault(variant => variant.Info.Runtime?.DeviceType == DeviceType.CPU);
            if (model == null || cpu == null)
            {
                return null;
            }

            model.SelectVariant(cpu);
            return model;
        }
        catch (Exception) when (!ct.IsCancellationRequested && Downloads.IsOffline)
        {
            throw new ModelDownloadRequiredException();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task EnsureCachedAsync(IModel model, IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        if (!await model.IsCachedAsync(ct).ConfigureAwait(false))
        {
            await Downloads.RunAsync(token => model.DownloadAsync(value => progress?.Report(new(
                AnalysisProgressStage.Preparing, double.IsFinite(value) ? Math.Clamp(value / 100d, 0, 1) : null)), token), ct)
                .ConfigureAwait(false);
        }
        progress?.Report(new(AnalysisProgressStage.Preparing, 1));
    }

    // Vision in SDK 1.2.4 uses its documented Responses endpoint. Bind only an OS-assigned
    // loopback port, and keep the listener alive only for this provider invocation.
    public Task<Uri> StartVisionServiceAsync(CancellationToken ct) => StartServiceAsync("/v1/responses", ct);
    public Task<Uri> StartChatServiceAsync(CancellationToken ct) => StartServiceAsync("/v1/chat/completions", ct);

    private async Task<Uri> StartServiceAsync(string path, CancellationToken ct)
    {
        if (!_ownsManager || _manager == null)
        {
            throw new InvalidOperationException("Inference requires an application-owned runtime.");
        }

        try
        {
            await _manager.StartWebServiceAsync(ct).ConfigureAwait(false);
            string address = _manager.Urls?.Single() ?? throw new InvalidOperationException("Inference endpoint is unavailable.");
            var endpoint = new Uri(address, UriKind.Absolute);
            if (endpoint.Scheme != "http" || endpoint.Host != "127.0.0.1" || endpoint.Port <= 0)
            {
                throw new InvalidOperationException("Inference endpoint must be local.");
            }

            return new Uri(endpoint, path);
        }
        catch
        {
            await StopServiceAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task StopServiceAsync() => _manager?.StopWebServiceAsync(CancellationToken.None) ?? Task.CompletedTask;

    public void Dispose()
    {
        if (_memoryPressure.IsValueCreated)
        {
            _memoryPressure.Value.Dispose();
        }

        if (_ownsManager)
        {
            _manager?.Dispose();
        }

        _gate.Dispose();
    }
}
