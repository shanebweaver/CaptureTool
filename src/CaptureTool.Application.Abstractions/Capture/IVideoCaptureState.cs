using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IVideoCaptureState
{
    event EventHandler<VideoFile>? NewVideoCaptured;
    event EventHandler? RecordingStarted;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<int>? DesktopAudioVolumeChanged;
    event EventHandler<bool>? AudioInputMutedStateChanged;
    event EventHandler<string?>? AudioInputSourceChanged;
    event EventHandler<bool>? PausedStateChanged;

    bool IsDesktopAudioEnabled { get; }
    int DesktopAudioVolumePercentage { get; }
    bool IsAudioInputMuted { get; }
    int AudioInputVolumePercentage { get; }
    bool IsRecording { get; }
    bool IsFinalizing { get; }
    bool IsPaused { get; }
    string? SelectedAudioInputSourceId { get; }
}
