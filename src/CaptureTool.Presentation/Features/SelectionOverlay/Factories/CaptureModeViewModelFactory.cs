using CaptureTool.Presentation.Features.SelectionOverlay;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Factories;
using CaptureTool.Infrastructure.Abstractions.Localization;

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
