using CaptureTool.Common.Commands;
using CaptureTool.Core.Implementations.Services.Navigation;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Shutdown;

namespace CaptureTool.Core.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayCloseAction : ActionCommand, ICaptureOverlayCloseAction
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly IAppNavigation _appNavigation;
    private readonly INavigationService _navigationService;
    private readonly IShutdownHandler _shutdownHandler;

    public CaptureOverlayCloseAction(
        IVideoCaptureHandler videoCaptureHandler,
        IAppNavigation appNavigation,
        INavigationService navigationService,
        IShutdownHandler shutdownHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _appNavigation = appNavigation;
        _navigationService = navigationService;
        _shutdownHandler = shutdownHandler;
    }

    public override bool CanExecute()
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

    public override void Execute()
    {
        try
        {
            _videoCaptureHandler.CancelVideoCapture();
        }
        catch { }

        if (_appNavigation.CanGoBack)
        {
            _appNavigation.GoBackToMainWindow();
        }
        else
        {
            _shutdownHandler.Shutdown();
        }
    }
}
