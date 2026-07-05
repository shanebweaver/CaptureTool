using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IAudioCaptureHandler
{
    event EventHandler<AudioCaptureState>? CaptureStateChanged;
    event EventHandler<bool>? MutedStateChanged;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<AudioFile>? NewAudioCaptured;

    bool IsRecording { get; }
    bool IsPaused { get; }
    bool IsMuted { get; }
    bool IsDesktopAudioEnabled { get; }
    AudioCaptureState CaptureState { get; }

    void StartCapture();
    void PauseCapture();
    AudioFile StopCapture();
    void SelectAudioInputSource(string? sourceId);
    void ToggleLocalAudio();
    void ToggleMute();
}
