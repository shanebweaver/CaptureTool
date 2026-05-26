using CaptureTool.Application.Abstractions.AudioCapture;

namespace CaptureTool.Application.AudioCapture;

public sealed partial class AudioCaptureToggleLocalAudioAppCommand : IAudioCaptureToggleLocalAudioAppCommand
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;

    public AudioCaptureToggleLocalAudioAppCommand(IAudioCaptureHandler audioCaptureHandler)
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
        _audioCaptureHandler.ToggleLocalAudio();
    }
}
