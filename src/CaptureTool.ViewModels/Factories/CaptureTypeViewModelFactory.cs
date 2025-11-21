using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;

namespace CaptureTool.ViewModels.Factories;

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
        return new(
            captureType,
            _localizationService);
    }
}