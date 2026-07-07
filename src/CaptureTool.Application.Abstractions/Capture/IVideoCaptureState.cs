using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IVideoCaptureState
{
    event EventHandler<VideoFile>? NewVideoCaptured;
    event EventHandler? RecordingStarted;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<bool>? PausedStateChanged;

    bool IsDesktopAudioEnabled { get; }
    bool IsAudioInputMuted { get; }
    int AudioInputVolumePercentage { get; }
    bool IsRecording { get; }
    bool IsFinalizing { get; }
    bool IsPaused { get; }
    string? SelectedAudioInputSourceId { get; }
}
