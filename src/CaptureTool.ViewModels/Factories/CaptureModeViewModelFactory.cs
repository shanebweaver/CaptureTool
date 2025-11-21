using CaptureTool.Capture;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class CaptureModeViewModelFactory : IFactoryServiceWithArgs<CaptureModeViewModel, CaptureMode>
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
