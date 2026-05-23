using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Factories;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Presentation.ViewModels.Factories;

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
        return new CaptureModeViewModel(
            captureMode,
            _localizationService);
    }
}
