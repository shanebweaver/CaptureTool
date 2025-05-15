using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Telemetry;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppLanguageViewModelFactory : IFactoryService<AppLanguageViewModel, string>
{
    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    public AppLanguageViewModelFactory(
        ITelemetryService telemetryService, 
        ICancellationService cancellationService)
    {
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
    }

    public async Task<AppLanguageViewModel> CreateAsync(string language, CancellationToken cancellationToken)
    {
        AppLanguageViewModel vm = new(
            _telemetryService,
            _cancellationService);
        await vm.LoadAsync(language, cancellationToken);
        return vm;
    }
}
