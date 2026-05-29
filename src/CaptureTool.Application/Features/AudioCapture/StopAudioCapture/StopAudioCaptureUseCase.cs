using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.StopAudioCapture;

public sealed class StopAudioCaptureUseCase : IUseCase<StopAudioCaptureRequest, StopAudioCaptureResponse>
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StopAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<StopAudioCaptureResponse> ExecuteAsync(StopAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        _audioCaptureHandler.StopCapture();
        return Task.FromResult(new StopAudioCaptureResponse());
    }
}
