using CaptureTool.Application.Abstractions.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.UseCases;

namespace CaptureTool.Application.UseCases.AudioCapture;

public sealed partial class AudioCaptureMuteUseCase : UseCase, IAudioCaptureMuteUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCaptureMuteUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public override void Execute()
    {
        _audioCaptureHandler.ToggleMute();
    }
}
