using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Factories;

namespace CaptureTool.Presentation.Features.SelectionOverlay.Factories;

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
