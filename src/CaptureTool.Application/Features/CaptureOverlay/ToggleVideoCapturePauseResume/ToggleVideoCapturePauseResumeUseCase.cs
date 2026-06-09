using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCapturePauseResume;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;

public sealed class ToggleVideoCapturePauseResumeUseCase : IToggleVideoCapturePauseResumeUseCase
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCapturePauseResumeUseCase(IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public bool CanExecute(ToggleVideoCapturePauseResumeRequest request)
    {
        return _videoCaptureHandler.IsRecording;
    }

    public Task<ToggleVideoCapturePauseResumeResponse> ExecuteAsync(ToggleVideoCapturePauseResumeRequest request, CancellationToken cancellationToken = default)
    {
        bool newValue = !_videoCaptureHandler.IsPaused;
        _videoCaptureHandler.ToggleIsPaused(newValue);
        return Task.FromResult(new ToggleVideoCapturePauseResumeResponse());
    }
}