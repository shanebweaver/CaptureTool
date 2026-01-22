using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Implementations.Actions.CaptureOverlay;

public sealed partial class CaptureOverlayTogglePauseResumeAction : ActionCommand, ICaptureOverlayTogglePauseResumeAction
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayTogglePauseResumeAction(
        IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    override public bool CanExecute()
    {
        return _videoCaptureHandler.IsRecording;
    }

    public override void Execute()
    {
        bool newValue = !_videoCaptureHandler.IsPaused;
        _videoCaptureHandler.ToggleIsPaused(newValue);
    }
}