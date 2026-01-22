using CaptureTool.Common.Commands;
using CaptureTool.Application.Implementations.Services.Navigation;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayStopVideoCaptureUseCase : ActionCommand, ICaptureOverlayStopVideoCaptureUseCase
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayStopVideoCaptureUseCase(
        INavigationService navigationService,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public override bool CanExecute()
    {
        if (_navigationService.CurrentRequest?.Route is not CaptureToolNavigationRoute route
            || route != CaptureToolNavigationRoute.VideoCapture)
        {
            return false;
        }
        return true;
    }

    public override void Execute()
    {
        var pendingVideo = _videoCaptureHandler.StopVideoCapture();
        _appNavigation.GoToVideoEdit(pendingVideo);
    }
}
