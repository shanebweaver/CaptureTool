using CaptureTool.Application.Abstractions.Storage;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal sealed class FoundryRuntime(IStorageService storage) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FoundryLocalManager? _manager;
    private bool _ownsManager;

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

    public void Dispose()
    {
        if (_ownsManager) _manager?.Dispose();
        _gate.Dispose();
    }
}
