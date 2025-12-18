using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public partial interface IVideoCaptureHandler
{
    event EventHandler<IVideoFile>? NewVideoCaptured;
    event EventHandler<bool>? DesktopAudioStateChanged;
    event EventHandler<bool>? PausedStateChanged;

    bool IsDesktopAudioEnabled { get; }
    bool IsMicrophoneEnabled { get; }
    bool IsRecording { get; }
    bool IsPaused { get; }

    void SetIsDesktopAudioEnabled(bool value);
    void SetIsMicrophoneEnabled(bool value);
    void ToggleDesktopAudioCapture(bool enabled);

    void StartVideoCapture(NewCaptureArgs args);
    IVideoFile StopVideoCapture();
    void CancelVideoCapture();
    void ToggleIsPaused(bool isPaused);

    // Audio Mixer Configuration (Phase 3)
    void SetAudioSourceTrack(int sourceId, int trackIndex);
    int GetAudioSourceTrack(int sourceId);
    void SetAudioSourceVolume(int sourceId, float volume);
    float GetAudioSourceVolume(int sourceId);
    void SetAudioSourceMuted(int sourceId, bool muted);
    bool IsAudioSourceMuted(int sourceId);
    void SetAudioTrackName(int trackIndex, string trackName);
    void SetAudioMixingMode(bool mixedMode);
    bool GetAudioMixingMode();
}
