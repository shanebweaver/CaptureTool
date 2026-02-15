namespace CaptureTool.Domain.Audio.Interfaces;

public interface IAudioCaptureService
{
    event EventHandler<bool>? PlayingStateChanged;
    event EventHandler<bool>? PausedStateChanged;
    event EventHandler<bool>? MutedStateChanged;
    event EventHandler<bool>? DesktopAudioStateChanged;

    bool IsPlaying { get; }
    bool IsPaused { get; }
    bool IsMuted { get; }
    bool IsDesktopAudioEnabled { get; }

    void Play();
    void Stop();
    void Pause();
    void ToggleMute();
    void ToggleDesktopAudio();
}
