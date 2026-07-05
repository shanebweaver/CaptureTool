using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Domain.Capture;

public interface IAudioRecorder
{
    void Pause();
    void Resume();
    void StartCapture(string outputPath);
    AudioFile StopCapture();
    void SetAudioInputSource(string? sourceId);
    void ToggleDesktopAudio();
    void ToggleMute();
}
