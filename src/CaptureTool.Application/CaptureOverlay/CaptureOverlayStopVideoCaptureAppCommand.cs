using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.CaptureOverlay;

public sealed partial class CaptureOverlayStopVideoCaptureAppCommand : ICaptureOverlayStopVideoCaptureAppCommand
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayStopVideoCaptureAppCommand(
        INavigationService navigationService,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        if (_navigationService.CurrentRequest?.Route is not CaptureToolNavigationRoute route
            || route != CaptureToolNavigationRoute.VideoCapture)
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
