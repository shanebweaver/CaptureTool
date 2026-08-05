using CaptureTool.Application.Abstractions.Capture.Video.StopVideoCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Video.StopVideoCapture;

internal sealed class StopVideoCaptureUseCase : IStopVideoCaptureUseCase
{
    private const string ActivityId = "StopVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public StopVideoCaptureUseCase(INavigationService navigationService,
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _videoCaptureWorkflow = videoCaptureWorkflow;
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
                var pendingVideo = _videoCaptureWorkflow.StopVideoCapture();
                // This is the successful completion of the active video workflow,
                // so it intentionally bypasses user-initiated leave confirmation.
                _navigationService.Navigate(NavigationRoute.VideoEdit, pendingVideo);
                return new StopVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
