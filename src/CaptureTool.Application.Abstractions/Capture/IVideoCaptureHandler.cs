using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IVideoCaptureHandler
{
    event EventHandler<IVideoFile>? NewVideoCaptured;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<bool>? PausedStateChanged;

    bool IsDesktopAudioEnabled { get; }
    bool IsRecording { get; }
    bool IsFinalizing { get; }
    bool IsPaused { get; }

    void PrepareForVideoCapture();
    void StartVideoCapture(NewCaptureArgs args);
    PendingVideoFile StopVideoCapture();
    void CancelVideoCapture();
    void SetIsDesktopAudioEnabled(bool value);
    void ToggleDesktopAudioCapture(bool enabled);
    void ToggleIsPaused(bool isPaused);
}
