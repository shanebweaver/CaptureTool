using CaptureTool.Capture;
using CaptureTool.Services;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class CaptureTypeViewModelFactory : IFactoryService<CaptureTypeViewModel, CaptureType>
{
    private readonly ILocalizationService _localizationService;
    public CaptureTypeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public CaptureTypeViewModel Create(CaptureType captureType)
    {
        return new(
            captureType,
            _localizationService);
    }
}