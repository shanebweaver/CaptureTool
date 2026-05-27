using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

internal class StartVideoCaptureAppCommand : IStartVideoCaptureAppCommand
{
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StartVideoCaptureAppCommand(
        INavigationService navigationService,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute(NewCaptureArgs args)
    {
        // Only allow starting when current route is VideoCapture overlay
        if (_navigationService.CurrentRequest?.Route is not NavigationRoute route
            || route != NavigationRoute.CaptureOverlay)
        {
            return false;
        }

        return true;
    }

    public void Execute(NewCaptureArgs args)
    {
        _videoCaptureHandler.StartVideoCapture(args);
        _navigationService.Navigate(NavigationRoute.CaptureOverlay, args);
    }
}
