using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IAudioCaptureState
{
    event EventHandler<AudioCaptureState>? CaptureStateChanged;
    event EventHandler<bool>? MutedStateChanged;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<AudioFile>? NewAudioCaptured;

    bool IsRecording { get; }
    bool IsPaused { get; }
    bool IsMuted { get; }
    bool IsDesktopAudioEnabled { get; }
    string? SelectedAudioInputSourceId { get; }
    AudioCaptureState CaptureState { get; }
}
