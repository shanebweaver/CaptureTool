using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

public sealed partial class AudioCaptureMuteAppCommand : IAudioCaptureMuteAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCaptureMuteAppCommand(IAudioCaptureHandler audioCaptureHandler)
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
        _audioCaptureHandler.ToggleMute();
    }
}
