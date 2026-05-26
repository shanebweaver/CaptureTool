using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

internal class ToggleLocalAudioCaptureAppCommand : IToggleLocalAudioCaptureAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public ToggleLocalAudioCaptureAppCommand(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.ToggleLocalAudio();
    }
}
