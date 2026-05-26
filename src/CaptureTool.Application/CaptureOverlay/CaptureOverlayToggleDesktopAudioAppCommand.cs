using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;

namespace CaptureTool.Application.CaptureOverlay;

public sealed partial class CaptureOverlayToggleDesktopAudioAppCommand : ICaptureOverlayToggleDesktopAudioAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayToggleDesktopAudioAppCommand(
        IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        bool newValue = !_videoCaptureHandler.IsDesktopAudioEnabled;
        _videoCaptureHandler.SetIsDesktopAudioEnabled(newValue);
        _videoCaptureHandler.ToggleDesktopAudioCapture(newValue);
    }
}
