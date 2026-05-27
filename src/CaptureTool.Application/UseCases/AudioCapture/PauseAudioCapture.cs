using CaptureTool.Application.Abstractions.Messaging.Commands;

namespace CaptureTool.Application.UseCases.AudioCapture;

public class PauseAudioCapture : IAppCommand
{
    private readonly AudioCaptureHandler _audioCaptureHandler;

    public PauseAudioCapture(AudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.PauseCapture();
    }
}
