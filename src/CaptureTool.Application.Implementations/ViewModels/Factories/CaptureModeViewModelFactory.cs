using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Implementations.ViewModels.Factories;

public sealed partial class CaptureModeViewModelFactory : IFactoryServiceWithArgs<ICaptureModeViewModel, CaptureMode>
{
    private readonly ILocalizationService _localizationService;
    public CaptureModeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public ICaptureModeViewModel Create(CaptureMode captureMode)
    {
        return new CaptureModeViewModel(
            captureMode,
            _localizationService);
    }
}
