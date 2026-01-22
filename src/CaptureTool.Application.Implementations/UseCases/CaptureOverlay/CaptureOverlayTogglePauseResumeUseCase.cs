using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Implementations.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayTogglePauseResumeUseCase : ActionCommand, ICaptureOverlayTogglePauseResumeUseCase
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayTogglePauseResumeUseCase(
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