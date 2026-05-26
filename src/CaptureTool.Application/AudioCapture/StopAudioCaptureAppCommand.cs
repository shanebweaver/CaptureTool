using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

internal class StopAudioCaptureAppCommand : IStopAudioCaptureAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public StopAudioCaptureAppCommand(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _audioCaptureHandler.StopCapture();
    }
}
