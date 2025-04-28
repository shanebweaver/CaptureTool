using CaptureTool.Capture.Desktop;
using CaptureTool.Services;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class DesktopCaptureModeViewModelFactory : IFactoryService<DesktopCaptureModeViewModel, DesktopCaptureMode>
{
    private readonly ILocalizationService _localizationService;

    public DesktopCaptureModeViewModelFactory(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public DesktopCaptureModeViewModel Create(DesktopCaptureMode desktopCaptureMode)
    {
        return new(_localizationService)
        {
            CaptureMode = desktopCaptureMode
        };
    }
}
