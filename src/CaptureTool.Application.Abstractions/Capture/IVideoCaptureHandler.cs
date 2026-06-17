using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IVideoCaptureHandler
{
    event EventHandler<IVideoFile>? NewVideoCaptured;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<bool>? PausedStateChanged;

    bool IsDesktopAudioEnabled { get; }
    bool IsAudioInputMuted { get; }
    bool IsRecording { get; }
    bool IsFinalizing { get; }
    bool IsPaused { get; }
    string? SelectedAudioInputSourceId { get; }

    void PrepareForVideoCapture();
    void StartVideoCapture(NewCaptureArgs args);
    PendingVideoFile StopVideoCapture();
    void CancelVideoCapture();
    void SetIsDesktopAudioEnabled(bool value);
    void SetIsAudioInputMuted(bool value);
    void SelectAudioInputSource(string? sourceId);
    void ToggleDesktopAudioCapture(bool enabled);
    void ToggleIsPaused(bool isPaused);
}
