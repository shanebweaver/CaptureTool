using CaptureTool.Application.Abstractions.Messaging.Commands;

namespace CaptureTool.Application.UseCases.AudioCapture;

internal class ToggleLocalAudioCapture : IAppCommand
{
    private readonly AudioCaptureHandler _audioCaptureHandler;

    public ToggleLocalAudioCapture(AudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.ToggleLocalAudio();
    }
}
