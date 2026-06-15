using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;

namespace CaptureTool.Application.Features.AudioCapture.StartAudioCapture;

public sealed class StartAudioCaptureUseCase : IStartAudioCaptureUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StartAudioCaptureUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public Task<StartAudioCaptureResponse> ExecuteAsync(StartAudioCaptureRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _audioCaptureHandler.StartCapture();
            return Task.FromResult(new StartAudioCaptureResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new StartAudioCaptureResponse(false));
        }
    }
}
