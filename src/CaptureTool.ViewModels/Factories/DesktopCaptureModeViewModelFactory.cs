using CaptureTool.Capture.Desktop;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Telemetry;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class DesktopCaptureModeViewModelFactory : IFactoryService<DesktopCaptureModeViewModel, DesktopCaptureMode>
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "Load";
        public static readonly string Unload = "Unload";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly ICancellationService _cancellationService;
    private readonly ILocalizationService _localizationService;

    public DesktopCaptureModeViewModelFactory(
        ITelemetryService telemetryService,
        ICancellationService cancellationService, 
        ILocalizationService localizationService)
    {
        _telemetryService = telemetryService;
        _cancellationService = cancellationService;
        _localizationService = localizationService;
    }

    public async Task<DesktopCaptureModeViewModel> CreateAsync(DesktopCaptureMode desktopCaptureMode, CancellationToken cancellationToken)
    {
        DesktopCaptureModeViewModel vm = new(
            _telemetryService,
            _cancellationService,
            _localizationService);
        await vm.LoadAsync(desktopCaptureMode, cancellationToken);
        return vm;
    }
}
