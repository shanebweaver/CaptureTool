using CaptureTool.Application.Interfaces.UseCases.AudioCapture;
using CaptureTool.Domain.Audio.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.Implementations.UseCases.AudioCapture;

public sealed class AudioCaptureStopUseCase : UseCase, IAudioCaptureStopUseCase
{
    private readonly IAudioCaptureService _audioCaptureService;

    public AudioCaptureStopUseCase(IAudioCaptureService audioCaptureService)
    {
        _audioCaptureService = audioCaptureService;
    }

    public override void Execute()
    {
        _audioCaptureService.Stop();
    }
}
