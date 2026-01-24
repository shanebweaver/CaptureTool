using CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.Implementations.UseCases.CaptureOverlay;

public sealed partial class CaptureOverlayTogglePauseResumeUseCase : UseCase, ICaptureOverlayTogglePauseResumeUseCase
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