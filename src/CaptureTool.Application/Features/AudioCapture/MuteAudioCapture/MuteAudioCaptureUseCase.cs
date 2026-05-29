using CaptureTool.Application.Abstractions;

namespace CaptureTool.Application.Features.AudioCapture.MuteAudioCapture;

public sealed class MuteAudioCaptureUseCase : IUseCase<MuteAudioCaptureRequest, MuteAudioCaptureResponse>
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public MuteAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<MuteAudioCaptureResponse> ExecuteAsync(MuteAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        _audioCaptureHandler.ToggleMute();
        return Task.FromResult(new MuteAudioCaptureResponse());
    }
}
