using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayToggleDesktopAudioAction : ActionCommand, ICaptureOverlayToggleDesktopAudioAction
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayToggleDesktopAudioAction(
        IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public override void Execute()
    {
        bool newValue = !_videoCaptureHandler.IsDesktopAudioEnabled;
        _videoCaptureHandler.SetIsDesktopAudioEnabled(newValue);
        _videoCaptureHandler.ToggleDesktopAudioCapture(newValue);
    }
}
