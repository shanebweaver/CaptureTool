using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;

namespace CaptureTool.Application.CaptureOverlay;

public sealed partial class CaptureOverlayTogglePauseResumeAppCommand : ICaptureOverlayTogglePauseResumeAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public CaptureOverlayTogglePauseResumeAppCommand(IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return _videoCaptureHandler.IsRecording;
    }

    public void Execute()
    {
        bool newValue = !_videoCaptureHandler.IsPaused;
        _videoCaptureHandler.ToggleIsPaused(newValue);
    }
}