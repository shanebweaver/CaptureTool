using CaptureTool.Application.Abstractions.UseCases.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Infrastructure.UseCases;

namespace CaptureTool.Application.UseCases.CaptureOverlay;

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