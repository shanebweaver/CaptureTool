using CaptureTool.Application.Abstractions.CaptureOverlay;
using CaptureTool.Application.Abstractions.VideoCapture;

namespace CaptureTool.Application.CaptureOverlay;

internal class ToggleVideoCapturePauseResumeAppCommand : IToggleVideoCapturePauseResumeAppCommand
{
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCapturePauseResumeAppCommand(IVideoCaptureHandler videoCaptureHandler)
    {
        _videoCaptureHandler = videoCaptureHandler;
    }

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