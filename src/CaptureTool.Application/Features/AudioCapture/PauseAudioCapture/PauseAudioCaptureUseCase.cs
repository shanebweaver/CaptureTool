using CaptureTool.Application.Abstractions;

namespace CaptureTool.Application.Features.AudioCapture.PauseAudioCapture;

public sealed class PauseAudioCaptureUseCase : IUseCase<PauseAudioCaptureRequest, PauseAudioCaptureResponse>
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public PauseAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<PauseAudioCaptureResponse> ExecuteAsync(PauseAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        _audioCaptureHandler.PauseCapture();
        return Task.FromResult(new PauseAudioCaptureResponse());
    }
}
