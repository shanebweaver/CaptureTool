using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Implementations.ViewModels.Factories;

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
