using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;

namespace CaptureTool.Application.Features.AudioCapture.MuteAudioCapture;

public sealed class MuteAudioCaptureUseCase : IMuteAudioCaptureUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public MuteAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<MuteAudioCaptureResponse> ExecuteAsync(MuteAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _audioCaptureHandler.ToggleMute();
            return Task.FromResult(new MuteAudioCaptureResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new MuteAudioCaptureResponse(false));
        }
    }
}
