using CaptureTool.Application.Interfaces.UseCases.AudioCapture;
using CaptureTool.Domain.Audio.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.Implementations.UseCases.AudioCapture;

public sealed class AudioCaptureMuteUseCase : UseCase, IAudioCaptureMuteUseCase
{
    private readonly IAudioCaptureService _audioCaptureService;

    public AudioCaptureMuteUseCase(IAudioCaptureService audioCaptureService)
    {
        _audioCaptureService = audioCaptureService;
    }

    public override void Execute()
    {
        _audioCaptureService.Mute();
    }
}
