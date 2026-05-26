using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

internal class StartAudioCaptureAppCommand : IStartAudioCaptureAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StartAudioCaptureAppCommand(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.StartCapture();
    }
}
