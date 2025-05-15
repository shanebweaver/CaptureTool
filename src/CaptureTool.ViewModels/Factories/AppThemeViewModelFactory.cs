using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Telemetry;
using CaptureTool.Services.Themes;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppThemeViewModelFactory : IFactoryService<AppThemeViewModel, AppTheme>
{
    private readonly ILocalizationService _localizationService;
    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;

    public AppThemeViewModelFactory(
        ILocalizationService localizationService,
        ITelemetryService telemetryService,
        ICancellationService cancellationService)
    {
        _localizationService = localizationService;
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
    }

    public async Task<AppThemeViewModel> CreateAsync(AppTheme appTheme, CancellationToken cancellationToken)
    {
        AppThemeViewModel vm = new(
            _localizationService,
            _telemetryService,
            _cancellationService);
        await vm.LoadAsync(appTheme, cancellationToken);
        return vm;
    }
}
