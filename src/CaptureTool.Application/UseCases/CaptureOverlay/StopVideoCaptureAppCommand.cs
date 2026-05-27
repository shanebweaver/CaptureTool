using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

internal class StopVideoCaptureAppCommand : IStopVideoCaptureAppCommand
{
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StopVideoCaptureAppCommand(
        INavigationService navigationService,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute()
    {
        return _navigationService.CurrentRequest?.Route is NavigationRoute route
            && route == NavigationRoute.CaptureOverlay;
    }

    public void Execute()
    {
        var pendingVideo = _videoCaptureHandler.StopVideoCapture();
        _navigationService.Navigate(NavigationRoute.VideoEdit, pendingVideo);
    }
}
