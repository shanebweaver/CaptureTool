using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IAudioCaptureHandler
{
    event EventHandler<AudioCaptureState>? CaptureStateChanged;
    event EventHandler<bool>? MutedStateChanged;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<IAudioFile>? NewAudioCaptured;

    bool IsRecording { get; }
    bool IsPaused { get; }
    bool IsMuted { get; }
    bool IsDesktopAudioEnabled { get; }
    AudioCaptureState CaptureState { get; }

    void StartCapture();
    void PauseCapture();
    IAudioFile StopCapture();
    void ToggleLocalAudio();
    void ToggleMute();
}
