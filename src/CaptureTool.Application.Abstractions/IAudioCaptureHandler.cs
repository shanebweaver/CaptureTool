using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Abstractions;

public interface IAudioCaptureHandler
{
    event EventHandler<AudioCaptureState>? CaptureStateChanged;
    event EventHandler<bool>? MutedStateChanged;
    event EventHandler<bool>? DesktopAudioStateChanged;

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
