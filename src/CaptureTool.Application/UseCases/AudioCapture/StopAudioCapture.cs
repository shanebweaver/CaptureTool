using CaptureTool.Application.Abstractions.Messaging.Commands;

namespace CaptureTool.Application.UseCases.AudioCapture;

internal class StopAudioCapture : IAppCommand
{
    private readonly AudioCaptureHandler _audioCaptureHandler;

    public StopAudioCapture(AudioCaptureHandler audioCaptureHandler)
    {
        _audioCaptureHandler = audioCaptureHandler;
    }

    public void Execute()
    {
        _audioCaptureHandler.StopCapture();
    }
}
