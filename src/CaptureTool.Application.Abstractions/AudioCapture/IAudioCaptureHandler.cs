using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Abstractions.AudioCapture;

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
    void ToggleLocalAudio();
}
