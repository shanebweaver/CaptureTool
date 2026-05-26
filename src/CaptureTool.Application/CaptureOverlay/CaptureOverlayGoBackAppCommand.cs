using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.CaptureOverlay;

internal class CaptureOverlayGoBackAppCommand : ICaptureOverlayGoBackAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;

    public CaptureOverlayGoBackAppCommand(
        IVideoCaptureHandler videoCaptureHandler,
        INavigationService navigationService,
        IAppNavigation appNavigation)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _navigationService = navigationService;
        _appNavigation = appNavigation;
    }

    public bool CanExecute()
    {
        if (!_navigationService.CanGoBack)
        {
            return false;
        }

        if (_navigationService.CurrentRequest?.Route is not NavigationRoute route
            || route != NavigationRoute.VideoCapture)
        {
            return false;
        }

        return true;
    }

    public void Execute()
    {
        try
        {
            _videoCaptureHandler.CancelVideoCapture();
        }
        catch { }

        if (!_appNavigation.TryGoBack())
        {
            _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault, true);
        }
    }
}
