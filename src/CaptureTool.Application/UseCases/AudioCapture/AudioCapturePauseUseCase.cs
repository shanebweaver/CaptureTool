using CaptureTool.Application.Abstractions.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.UseCases.AudioCapture;

public sealed partial class AudioCapturePauseUseCase : UseCase, IAudioCapturePauseUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCapturePauseUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public override void Execute()
    {
        _audioCaptureHandler.PauseCapture();
    }
}
