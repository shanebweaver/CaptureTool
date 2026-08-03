using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Video.StartVideoCapture;

internal sealed class StartVideoCaptureUseCase : IStartVideoCaptureUseCase
{
    private const string ActivityId = "StartVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly INavigationService _navigationService;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public StartVideoCaptureUseCase(INavigationService navigationService,
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _navigationService = navigationService;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public bool CanExecute(StartVideoCaptureRequest request)
    {
        bool canExecute = _navigationService.CurrentRequest?.Route is NavigationRoute.CaptureOverlay;
        return canExecute;
    }

    public Task<UseCaseResponse<StartVideoCaptureResponse>> ExecuteAsync(StartVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                try
                {
                    _videoCaptureWorkflow.StartVideoCapture(request.CaptureArgs);
                    return new StartVideoCaptureResponse();
                }
                catch (VideoCaptureNotSupportedException)
                {
                    // The workflow already emitted bounded capture-failure telemetry.
                    // Return a structured result so presentation can show the right UX.
                    return new StartVideoCaptureResponse(
                        Succeeded: false,
                        FailureReason: StartVideoCaptureFailureReason.NotSupported);
                }
            },
            cancellationToken: cancellationToken);
    }
}
