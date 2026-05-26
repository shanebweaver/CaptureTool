using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.CaptureOverlay;

public sealed partial class CaptureOverlayCloseAppCommand : ICaptureOverlayCloseAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IShowMainWindowAppCommand _showMainWindowAppCommand;
    private readonly INavigationService _navigationService;
    private readonly IShutdownHandler _shutdownHandler;

    public CaptureOverlayCloseAppCommand(
        IVideoCaptureHandler videoCaptureHandler,
        IShowMainWindowAppCommand showMainWindowAppCommand,
        INavigationService navigationService,
        IShutdownHandler shutdownHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _showMainWindowAppCommand = showMainWindowAppCommand;
        _navigationService = navigationService;
        _shutdownHandler = shutdownHandler;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        if (!_navigationService.CanGoBack)
        {
            return false;
        }

        if (_navigationService.CurrentRequest?.Route is not CaptureToolNavigationRoute route
            || route != CaptureToolNavigationRoute.VideoCapture)
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

        if (_showMainWindowAppCommand.CanExecute())
        {
            _showMainWindowAppCommand.Execute();
        }
        else
        {
            _shutdownHandler.Shutdown();
        }
    }
}
