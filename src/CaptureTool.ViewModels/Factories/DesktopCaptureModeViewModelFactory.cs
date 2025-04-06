using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class DesktopCaptureModeViewModelFactory : IFactoryService<DesktopCaptureModeViewModel>
{
    private readonly ICancellationService _cancellationService;
    private readonly ILocalizationService _localizationService;

    public DesktopCaptureModeViewModelFactory(
        ICancellationService cancellationService,
        ILocalizationService localizationService)
    {
        _cancellationService = cancellationService;
        _localizationService = localizationService;
    }

    public DesktopCaptureModeViewModel Create()
    {
        return new(
            _cancellationService,
            _localizationService);
    }
}