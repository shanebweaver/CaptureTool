using CaptureTool.Application.Abstractions.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.UseCases;

namespace CaptureTool.Application.UseCases.AudioCapture;

public sealed partial class AudioCaptureStartUseCase : UseCase, IAudioCaptureStartUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCaptureStartUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public override void Execute()
    {
        _audioCaptureHandler.StartCapture();
    }
}
