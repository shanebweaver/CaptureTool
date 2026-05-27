using CaptureTool.Application.Abstractions.Messaging.Commands;

namespace CaptureTool.Application.UseCases.AudioCapture;

public class MuteAudioCapture : IAppCommand
{
    private readonly AudioCaptureHandler _audioCaptureHandler;

    public MuteAudioCapture(AudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.ToggleMute();
    }
}
