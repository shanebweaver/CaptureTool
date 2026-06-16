using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.StopVideoCapture;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.StopVideoCapture;

public sealed class StopVideoCaptureUseCase : IStopVideoCaptureUseCase
{
    private const string ActivityId = "StopVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public StopVideoCaptureUseCase(INavigationService navigationService,
        IVideoCaptureHandler videoCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute(StopVideoCaptureRequest request)
    {
        bool canExecute = _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;
        return canExecute;
    }

    public Task<UseCaseResponse<StopVideoCaptureResponse>> ExecuteAsync(StopVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var pendingVideo = _videoCaptureHandler.StopVideoCapture();
                _navigationService.Navigate(NavigationRoute.VideoEdit, pendingVideo);
                return new StopVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
