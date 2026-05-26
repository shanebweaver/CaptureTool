using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

internal class MuteAudioCaptureAppCommand : IMuteAudioCaptureAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public MuteAudioCaptureAppCommand(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.ToggleMute();
    }
}
