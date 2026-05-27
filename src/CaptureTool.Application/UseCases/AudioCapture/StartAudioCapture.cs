using CaptureTool.Application.Abstractions.Messaging.Commands;

namespace CaptureTool.Application.UseCases.AudioCapture;

public class StartAudioCapture : IAppCommand
{
    private readonly AudioCaptureHandler _audioCaptureHandler;

    public StartAudioCapture(AudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.StartCapture();
    }
}
