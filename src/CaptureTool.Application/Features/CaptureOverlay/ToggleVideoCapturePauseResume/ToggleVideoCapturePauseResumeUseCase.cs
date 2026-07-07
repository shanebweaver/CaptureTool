using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;

internal sealed class ToggleVideoCapturePauseResumeUseCase : IToggleVideoCapturePauseResumeUseCase
{
    private const string ActivityId = "ToggleVideoCapturePauseResume";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public ToggleVideoCapturePauseResumeUseCase(IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public bool CanExecute(ToggleVideoCapturePauseResumeRequest request)
    {
        return _videoCaptureWorkflow.IsRecording;
    }

    public Task<UseCaseResponse<ToggleVideoCapturePauseResumeResponse>> ExecuteAsync(ToggleVideoCapturePauseResumeRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                bool newValue = !_videoCaptureWorkflow.IsPaused;
                _videoCaptureWorkflow.ToggleIsPaused(newValue);
                return new ToggleVideoCapturePauseResumeResponse();
            },
            cancellationToken: cancellationToken);
    }
}
