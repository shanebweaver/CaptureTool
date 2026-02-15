using CaptureTool.Application.Interfaces.UseCases.AudioCapture;
using CaptureTool.Domain.Audio.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.Implementations.UseCases.AudioCapture;

public sealed partial class AudioCapturePlayUseCase : UseCase, IAudioCapturePlayUseCase
{
    private readonly IAudioCaptureService _audioCaptureService;

    public AudioCapturePlayUseCase(IAudioCaptureService audioCaptureService)
    {
        _audioCaptureService = audioCaptureService;
    }

    public override void Execute()
    {
        _audioCaptureService.Play();
    }
}
