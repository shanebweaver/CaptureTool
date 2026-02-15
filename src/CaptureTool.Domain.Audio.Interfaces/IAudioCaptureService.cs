namespace CaptureTool.Domain.Audio.Interfaces;

public interface IAudioCaptureService
{
    void Play();
    void Stop();
    void Pause();
    void Mute();
    void ToggleDesktopAudio();
}
