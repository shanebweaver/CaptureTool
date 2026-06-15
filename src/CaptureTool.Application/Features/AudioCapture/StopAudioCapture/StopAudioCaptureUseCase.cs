using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;

namespace CaptureTool.Application.Features.AudioCapture.StopAudioCapture;

public sealed class StopAudioCaptureUseCase : IStopAudioCaptureUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StopAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<StopAudioCaptureResponse> ExecuteAsync(StopAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _audioCaptureHandler.StopCapture();
            return Task.FromResult(new StopAudioCaptureResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new StopAudioCaptureResponse(false));
        }
    }
}
