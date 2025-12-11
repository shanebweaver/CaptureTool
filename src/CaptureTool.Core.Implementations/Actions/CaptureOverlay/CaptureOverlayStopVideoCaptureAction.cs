using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Navigation;

namespace CaptureTool.Core.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayStopVideoCaptureAction : ActionCommand, ICaptureOverlayStopVideoCaptureAction
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayStopVideoCaptureAction(
        INavigationService navigationService,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public override bool CanExecute()
    {
        if (_navigationService.CurrentRequest?.Route is not Core.Implementations.Navigation.CaptureToolNavigationRoute route
            || route != Core.Implementations.Navigation.CaptureToolNavigationRoute.VideoCapture)
        {
            return false;
        }
        return true;
    }

    public override void Execute()
    {
        var video = _videoCaptureHandler.StopVideoCapture();
        _appNavigation.GoToVideoEdit(video);
    }
}
