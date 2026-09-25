using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal sealed class FoundryRuntime(IStorageService storage) : IAnalysisResources, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _modelGate = new(1, 1);
    private IModel? _loadedModel;
    private bool _modelReady;
    private FoundryLocalManager? _manager;
    private bool _ownsManager;

    // One resident model, shared by all adapters. The lease spans native inference,
    // including an invocation that has outlived its worker deadline.
    internal async Task<IAsyncDisposable> AcquireModelAsync(IModel model, CancellationToken ct)
    {
        await _modelGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_modelReady || _loadedModel?.Id != model.Id)
            {
                await UnloadAsync().ConfigureAwait(false);
                _loadedModel = model;
                try { await model.LoadAsync(ct).ConfigureAwait(false); _modelReady = true; }
                catch { await UnloadAsync().ConfigureAwait(false); throw; }
            }
            ct.ThrowIfCancellationRequested();
            return new ModelLease(this, ct);
        }
        catch { _modelGate.Release(); throw; }
    }

    public async Task ReleaseAsync()
    {
        await _modelGate.WaitAsync().ConfigureAwait(false);
        try { await UnloadAsync().ConfigureAwait(false); }
        finally { _modelGate.Release(); }
    }

    private async Task UnloadAsync()
    {
        if (_loadedModel == null) return;
        _modelReady = false;
        // Keep ownership if unloading fails; never load a second model on top of it.
        await _loadedModel.UnloadAsync(CancellationToken.None).ConfigureAwait(false);
        _loadedModel = null;
    }

    private sealed class ModelLease(FoundryRuntime owner, CancellationToken ct) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { if (ct.IsCancellationRequested) await owner.UnloadAsync().ConfigureAwait(false); }
            finally { owner._modelGate.Release(); }
        }
    }

    // Only preparation may initialize the SDK or access its catalog. Passive probes never call here.
    public async Task<IModel?> ResolveAsync(string alias, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_manager == null)
            {
                if (!FoundryLocalManager.IsInitialized)
                {
                    string root = Path.Combine(storage.GetApplicationDataFolderPath(), "AnalysisModels", "FoundryLocal");
                    await FoundryLocalManager.CreateAsync(new Configuration
                    {
                        AppName = "capture-tool", AppDataDir = root, ModelCacheDir = Path.Combine(root, "models"),
                        LogsDir = Path.Combine(root, "logs"), LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Fatal,
                        Web = new Configuration.WebService { Urls = "http://127.0.0.1:0" },
                    }, NullLogger.Instance, ct).ConfigureAwait(false);
                    _ownsManager = true;
                }
                _manager = FoundryLocalManager.Instance;
            }
            ICatalog catalog = await _manager.GetCatalogAsync(ct).ConfigureAwait(false);
            IModel? model = await catalog.GetModelAsync(alias, ct).ConfigureAwait(false);
            IModel? cpu = model?.Variants.FirstOrDefault(variant => variant.Info.Runtime?.DeviceType == DeviceType.CPU);
            if (model == null || cpu == null) return null;
            model.SelectVariant(cpu);
            return model;
        }
        finally { _gate.Release(); }
    }

    // Vision in SDK 1.2.4 uses its documented Responses endpoint. Bind only an OS-assigned
    // loopback port, and keep the listener alive only for this provider invocation.
    public Task<Uri> StartVisionServiceAsync(CancellationToken ct) => StartServiceAsync("/v1/responses", ct);
    public Task<Uri> StartChatServiceAsync(CancellationToken ct) => StartServiceAsync("/v1/chat/completions", ct);

    private async Task<Uri> StartServiceAsync(string path, CancellationToken ct)
    {
        if (!_ownsManager || _manager == null) throw new InvalidOperationException("Inference requires an application-owned runtime.");
        try
        {
            await _manager.StartWebServiceAsync(ct).ConfigureAwait(false);
            string address = _manager.Urls?.Single() ?? throw new InvalidOperationException("Inference endpoint is unavailable.");
            var endpoint = new Uri(address, UriKind.Absolute);
            if (endpoint.Scheme != "http" || endpoint.Host != "127.0.0.1" || endpoint.Port <= 0)
                throw new InvalidOperationException("Inference endpoint must be local.");
            return new Uri(endpoint, path);
        }
        catch { await StopServiceAsync().ConfigureAwait(false); throw; }
    }

    public Task StopServiceAsync() => _manager?.StopWebServiceAsync(CancellationToken.None) ?? Task.CompletedTask;

    public void Dispose()
    {
        if (_ownsManager) _manager?.Dispose();
        _gate.Dispose();
    }
}
