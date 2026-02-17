using CaptureTool.Application.Interfaces.UseCases.AudioCapture;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.Implementations.UseCases.AudioCapture;

public sealed partial class AudioCaptureToggleDesktopAudioUseCase : UseCase, IAudioCaptureToggleDesktopAudioUseCase
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCaptureToggleDesktopAudioUseCase(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public override void Execute()
    {
        _audioCaptureHandler.ToggleDesktopAudio();
    }
}
