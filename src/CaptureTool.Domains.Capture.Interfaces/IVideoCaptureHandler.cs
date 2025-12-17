using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IVideoCaptureHandler
{
    event EventHandler<IVideoFile>? NewVideoCaptured;
    event EventHandler<bool>? DesktopAudioStateChanged;

    bool IsDesktopAudioEnabled { get; }
    bool IsRecording { get; }
    bool IsPaused { get; }

    void SetIsDesktopAudioEnabled(bool value);
    void ToggleDesktopAudioCapture(bool enabled);

    void StartVideoCapture(NewCaptureArgs args);
    IVideoFile StopVideoCapture();
    void CancelVideoCapture();
    void ToggleIsPaused(bool isPaused);
}
