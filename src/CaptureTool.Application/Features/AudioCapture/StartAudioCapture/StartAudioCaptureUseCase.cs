using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.AudioCapture.StartAudioCapture;

public sealed class StartAudioCaptureUseCase : IUseCase<StartAudioCaptureRequest, StartAudioCaptureResponse>
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StartAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<StartAudioCaptureResponse> ExecuteAsync(StartAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        _audioCaptureHandler.StartCapture();
        return Task.FromResult(new StartAudioCaptureResponse());
    }
}
