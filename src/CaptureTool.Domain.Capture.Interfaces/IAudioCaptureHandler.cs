using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domain.Capture.Interfaces;

public interface IAudioCaptureHandler
{
    bool IsDesktopAudioEnabled { get; }
    bool IsMuted { get; }
    AudioCaptureState CaptureState { get; }

    bool IsRecording => CaptureState == AudioCaptureState.Recording;
    bool IsStopped => CaptureState == AudioCaptureState.Stopped;
    bool IsPaused => CaptureState == AudioCaptureState.Paused;

    event EventHandler<AudioCaptureState>? CaptureStateChanged;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<bool>? MutedStateChanged;

    void StartCapture();
    IAudioFile StopCapture();
    void PauseCapture();
    void ToggleMute();
    void ToggleDesktopAudio();
}
