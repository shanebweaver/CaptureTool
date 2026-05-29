using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCapturePauseResume;

public sealed class ToggleVideoCapturePauseResumeUseCase : IUseCase<ToggleVideoCapturePauseResumeRequest, ToggleVideoCapturePauseResumeResponse>, IConditional<ToggleVideoCapturePauseResumeRequest>
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