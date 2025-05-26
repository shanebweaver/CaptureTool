using CaptureTool.Capture;
using CaptureTool.Services;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class DesktopCaptureModeViewModelFactory : IFactoryService<DesktopCaptureModeViewModel, CaptureMode>
{
    private readonly ILocalizationService _localizationService;

    public DesktopCaptureModeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public DesktopCaptureModeViewModel Create(CaptureMode desktopCaptureMode)
    {
        return new(
            desktopCaptureMode,
            _localizationService);
    }
}
