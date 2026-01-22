using CaptureTool.Common.Commands;
using CaptureTool.Application.Implementations.Services.Navigation;
using CaptureTool.Application.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayStartVideoCaptureAction : ActionCommand<NewCaptureArgs>, ICaptureOverlayStartVideoCaptureAction
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayStartVideoCaptureAction(
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
        _appNavigation.GoToVideoCapture(args);
        _videoCaptureHandler.StartVideoCapture(args);
    }
}
