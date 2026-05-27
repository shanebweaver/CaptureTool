using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.UseCases.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

internal class CaptureOverlayCloseAppCommand : ICaptureOverlayCloseAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IShowMainWindowAppCommand _showMainWindowAppCommand;
    private readonly INavigationService _navigationService;

    public CaptureOverlayCloseAppCommand(
        IVideoCaptureHandler videoCaptureHandler,
        IShowMainWindowAppCommand showMainWindowAppCommand,
        INavigationService navigationService)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _showMainWindowAppCommand = showMainWindowAppCommand;
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

        _showMainWindowAppCommand.Execute();
    }
}
