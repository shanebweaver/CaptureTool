using CaptureTool.Application.Services.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Navigation;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayStartVideoCaptureUseCase : UseCase<NewCaptureArgs>, ICaptureOverlayStartVideoCaptureUseCase
{
    private readonly INavigationService _navigationService;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayStartVideoCaptureUseCase(
        INavigationService navigationService,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public override bool CanExecute(NewCaptureArgs args)
    {
        // Only allow starting when current route is VideoCapture overlay
        if (_navigationService.CurrentRequest?.Route is not CaptureToolNavigationRoute route
            || route != CaptureToolNavigationRoute.VideoCapture)
        {
            return false;
        }

        return true;
    }

    public override void Execute(NewCaptureArgs args)
    {
        _videoCaptureHandler.StartVideoCapture(args);
        _appNavigation.GoToVideoCapture(args);
    }
}
