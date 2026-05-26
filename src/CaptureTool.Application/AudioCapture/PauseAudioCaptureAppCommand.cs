using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

internal class PauseAudioCaptureAppCommand : IPauseAudioCaptureAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public PauseAudioCaptureAppCommand(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.PauseCapture();
    }
}
