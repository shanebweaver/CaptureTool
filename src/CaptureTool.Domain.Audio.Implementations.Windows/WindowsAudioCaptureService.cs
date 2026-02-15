using CaptureTool.Domain.Audio.Interfaces;

namespace CaptureTool.Domain.Audio.Implementations.Windows;

public class WindowsAudioCaptureService : IAudioCaptureService
{
    public event EventHandler<bool>? PlayingStateChanged;
    public event EventHandler<bool>? PausedStateChanged;
    public event EventHandler<bool>? MutedStateChanged;
    public event EventHandler<bool>? DesktopAudioStateChanged;

    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsMuted { get; private set; }
    public bool IsDesktopAudioEnabled { get; private set; }

    public void Play()
    {
        // Empty implementation - to be implemented later
    }

    public void Stop()
    {
        // Empty implementation - to be implemented later
    }

    public void Pause()
    {
        // Empty implementation - to be implemented later
    }

    public void ToggleMute()
    {
        // Empty implementation - to be implemented later
    }

    public void ToggleDesktopAudio()
    {
        // Empty implementation - to be implemented later
    }
}
