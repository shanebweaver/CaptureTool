using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;

public sealed class ToggleVideoCapturePauseResumeUseCase : IToggleVideoCapturePauseResumeUseCase
{
    private const string ActivityId = "ToggleVideoCapturePauseResume";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCapturePauseResumeUseCase(IVideoCaptureHandler videoCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute(ToggleVideoCapturePauseResumeRequest request)
    {
        return _videoCaptureHandler.IsRecording;
    }

    public Task<UseCaseResponse<ToggleVideoCapturePauseResumeResponse>> ExecuteAsync(ToggleVideoCapturePauseResumeRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                bool newValue = !_videoCaptureHandler.IsPaused;
                _videoCaptureHandler.ToggleIsPaused(newValue);
                return new ToggleVideoCapturePauseResumeResponse();
            },
            cancellationToken: cancellationToken);
    }
}
