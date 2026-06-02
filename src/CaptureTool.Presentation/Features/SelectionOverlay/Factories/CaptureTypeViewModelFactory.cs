using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Presentation.Factories;

namespace CaptureTool.Presentation.Features.SelectionOverlay.Factories;

public sealed partial class CaptureTypeViewModelFactory : IFactoryServiceWithArgs<CaptureTypeViewModel, CaptureType>
{
    private readonly ILocalizationService _localizationService;
    public CaptureTypeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public CaptureTypeViewModel Create(CaptureType captureType)
    {
        return new CaptureTypeViewModel(
            captureType,
            _localizationService);
    }
}