using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IVideoCaptureHandler
{
    event EventHandler<IVideoFile>? NewVideoCaptured;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<bool>? PausedStateChanged;

    bool IsDesktopAudioEnabled { get; }
    bool IsRecording { get; }
    bool IsPaused { get; }

    void SetIsDesktopAudioEnabled(bool value);
    void ToggleDesktopAudioCapture(bool enabled);

    void StartVideoCapture(NewCaptureArgs args);
    PendingVideoFile StopVideoCapture();
    void CancelVideoCapture();
    void ToggleIsPaused(bool isPaused);
}
