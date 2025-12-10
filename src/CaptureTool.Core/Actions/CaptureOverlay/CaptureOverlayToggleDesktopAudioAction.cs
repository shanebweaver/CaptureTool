using CaptureTool.Common.Commands;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayToggleDesktopAudioAction : ActionCommand
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
    }
}
