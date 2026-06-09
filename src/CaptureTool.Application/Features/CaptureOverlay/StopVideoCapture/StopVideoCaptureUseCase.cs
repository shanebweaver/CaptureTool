using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;

namespace CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;

public sealed class StopVideoCaptureUseCase : IStopVideoCaptureUseCase
{
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StopVideoCaptureUseCase(
        INavigationService navigationService,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _navigationService = navigationService;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute(StopVideoCaptureRequest request)
    {
        bool canExecute = _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;
        return canExecute;
    }

    public Task<StopVideoCaptureResponse> ExecuteAsync(StopVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        var pendingVideo = _videoCaptureHandler.StopVideoCapture();
        _navigationService.Navigate(NavigationRoute.VideoEdit, pendingVideo);
        return Task.FromResult(new StopVideoCaptureResponse());
    }
}