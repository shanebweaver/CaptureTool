using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Interfaces.Factories;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Presentation.ViewModels.Factories;

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