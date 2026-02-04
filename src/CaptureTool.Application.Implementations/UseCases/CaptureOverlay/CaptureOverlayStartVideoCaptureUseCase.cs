using CaptureTool.Application.Implementations.Services.Navigation;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayStartVideoCaptureUseCase : UseCase<NewCaptureArgs>, ICaptureOverlayStartVideoCaptureUseCase
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayStartVideoCaptureUseCase(
        INavigationService navigationService,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public override bool CanExecute(NewCaptureArgs args)
    {
        // Only allow starting when current route is VideoCapture overlay
        if (_navigationService.CurrentRequest?.Route is not CaptureToolNavigationRoute route
            || route != CaptureToolNavigationRoute.VideoCapture)
        {
            return false;
        }

        return true;
    }

    public override void Execute(NewCaptureArgs args)
    {
        _videoCaptureHandler.StartVideoCapture(args);
        _appNavigation.GoToVideoCapture(args);
    }
}
