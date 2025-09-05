using CaptureTool.Capture;
using CaptureTool.Services;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class CaptureModeViewModelFactory : IFactoryService<CaptureModeViewModel, CaptureMode>
{
    private readonly ILocalizationService _localizationService;
    public CaptureModeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public CaptureModeViewModel Create(CaptureMode captureMode)
    {
        return new(
            captureMode,
            _localizationService);
    }
}
