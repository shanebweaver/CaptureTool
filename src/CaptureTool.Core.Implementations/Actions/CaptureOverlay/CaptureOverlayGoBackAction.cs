using CaptureTool.Common.Commands;
using CaptureTool.Core.Implementations.Services.Navigation;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Navigation;

namespace CaptureTool.Core.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayGoBackAction : ActionCommand, ICaptureOverlayGoBackAction
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;

    public CaptureOverlayGoBackAction(
        IVideoCaptureHandler videoCaptureHandler,
        INavigationService navigationService,
        IAppNavigation appNavigation)
    {
        _videoCaptureHandler = videoCaptureHandler;
        _navigationService = navigationService;
        _appNavigation = appNavigation;
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

        if (!_appNavigation.TryGoBack())
        {
            _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault, true);
        }
    }
}
