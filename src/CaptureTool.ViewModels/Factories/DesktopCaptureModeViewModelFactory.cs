using CaptureTool.Services;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class DesktopCaptureModeViewModelFactory : IFactoryService<DesktopCaptureModeViewModel>
{
    private readonly ILocalizationService _localizationService;

    public DesktopCaptureModeViewModelFactory(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public DesktopCaptureModeViewModel Create()
    {
        return new(_localizationService);
    }
}