using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

public sealed partial class AudioCapturePauseAppCommand : IAudioCapturePauseAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCapturePauseAppCommand(IAudioCaptureHandler audioCaptureHandler)
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
        _audioCaptureHandler.PauseCapture();
    }
}
