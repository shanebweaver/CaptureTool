using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;

namespace CaptureTool.Application.Features.AudioCapture.PauseAudioCapture;

public sealed class PauseAudioCaptureUseCase : IPauseAudioCaptureUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public PauseAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<PauseAudioCaptureResponse> ExecuteAsync(PauseAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _audioCaptureHandler.PauseCapture();
            return Task.FromResult(new PauseAudioCaptureResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new PauseAudioCaptureResponse(false));
        }
    }
}
