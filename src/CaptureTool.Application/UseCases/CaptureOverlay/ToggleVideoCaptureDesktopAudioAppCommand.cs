using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

internal class ToggleVideoCaptureDesktopAudioAppCommand : IToggleVideoCaptureDesktopAudioAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCaptureDesktopAudioAppCommand(
        IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public void Execute()
    {
        bool newValue = !_videoCaptureHandler.IsDesktopAudioEnabled;
        _videoCaptureHandler.SetIsDesktopAudioEnabled(newValue);
        _videoCaptureHandler.ToggleDesktopAudioCapture(newValue);
    }
}
