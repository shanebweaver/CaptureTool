using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Implementations.ViewModels.Factories;

public sealed partial class CaptureTypeViewModelFactory : IFactoryServiceWithArgs<ICaptureTypeViewModel, CaptureType>
{
    private readonly ILocalizationService _localizationService;
    public CaptureTypeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public ICaptureTypeViewModel Create(CaptureType captureType)
    {
        return new CaptureTypeViewModel(
            captureType,
            _localizationService);
    }
}