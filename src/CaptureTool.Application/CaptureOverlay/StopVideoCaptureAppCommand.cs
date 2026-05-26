using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.CaptureOverlay;

internal class StopVideoCaptureAppCommand : IStopVideoCaptureAppCommand
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StopVideoCaptureAppCommand(
        INavigationService navigationService,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute()
    {
        if (_navigationService.CurrentRequest?.Route is not NavigationRoute route
            || route != NavigationRoute.VideoCapture)
        {
            return false;
        }
        return true;
    }

    public void Execute()
    {
        var pendingVideo = _videoCaptureHandler.StopVideoCapture();
        _appNavigation.GoToVideoEdit(pendingVideo);
    }
}
