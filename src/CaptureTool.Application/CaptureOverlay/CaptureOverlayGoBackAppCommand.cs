using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.CaptureOverlay;

internal class CaptureOverlayGoBackAppCommand : ICaptureOverlayGoBackAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly INavigationService _navigationService;

    public CaptureOverlayGoBackAppCommand(
        IVideoCaptureHandler videoCaptureHandler,
        INavigationService navigationService)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _navigationService = navigationService;
    }

    public bool CanExecute()
    {
        if (!_navigationService.CanGoBack)
        {
            return false;
        }

        if (_navigationService.CurrentRequest?.Route is not NavigationRoute route
            || route != NavigationRoute.CaptureOverlay)
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

        if (!_navigationService.TryGoBack())
        {
            _navigationService.Navigate(NavigationRoute.SelectionOverlay, CaptureOptions.VideoDefault, true);
        }
    }
}
