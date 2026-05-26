using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

public sealed partial class AudioCaptureStopAppCommand : IAudioCaptureStopAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCaptureStopAppCommand(IAudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _audioCaptureHandler.StopCapture();
    }
}
