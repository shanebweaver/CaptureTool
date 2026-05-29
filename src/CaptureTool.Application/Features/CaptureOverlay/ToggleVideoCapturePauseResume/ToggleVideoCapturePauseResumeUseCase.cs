using CaptureTool.Application.Abstractions;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;

public sealed class ToggleVideoCapturePauseResumeUseCase : IUseCase<ToggleVideoCapturePauseResumeRequest, ToggleVideoCapturePauseResumeResponse>, IConditional<ToggleVideoCapturePauseResumeRequest>
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCapturePauseResumeUseCase(IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public Task<bool> CanExecuteAsync(ToggleVideoCapturePauseResumeRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_videoCaptureHandler.IsRecording);
    }

    public Task<ToggleVideoCapturePauseResumeResponse> ExecuteAsync(ToggleVideoCapturePauseResumeRequest request, CancellationToken cancellationToken = default)
    {
        bool newValue = !_videoCaptureHandler.IsPaused;
        _videoCaptureHandler.ToggleIsPaused(newValue);
        return Task.FromResult(new ToggleVideoCapturePauseResumeResponse());
    }
}