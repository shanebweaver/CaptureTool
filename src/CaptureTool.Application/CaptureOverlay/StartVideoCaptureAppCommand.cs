using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.CaptureOverlay;

internal class StartVideoCaptureAppCommand : IStartVideoCaptureAppCommand
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StartVideoCaptureAppCommand(
        INavigationService navigationService,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute(NewCaptureArgs args)
    {
        // Only allow starting when current route is VideoCapture overlay
        if (_navigationService.CurrentRequest?.Route is not NavigationRoute route
            || route != NavigationRoute.VideoCapture)
        {
            return false;
        }

        return true;
    }

    public void Execute(NewCaptureArgs args)
    {
        _videoCaptureHandler.StartVideoCapture(args);
        _appNavigation.GoToVideoCapture(args);
    }
}
