using CaptureTool.Application.Abstractions;

namespace CaptureTool.Application.Features.AudioCapture.ToggleLocalAudioCapture;

public sealed class ToggleLocalAudioCaptureUseCase : IUseCase<ToggleLocalAudioCaptureRequest, ToggleLocalAudioCaptureResponse>
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public ToggleLocalAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<ToggleLocalAudioCaptureResponse> ExecuteAsync(ToggleLocalAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        _audioCaptureHandler.ToggleLocalAudio();
        return Task.FromResult(new ToggleLocalAudioCaptureResponse());
    }
}
