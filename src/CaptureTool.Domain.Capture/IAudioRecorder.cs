using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Domain.Capture;

public interface IAudioRecorder
{
    void Pause();
    void Resume();
    void StartCapture(string outputPath);
    IAudioFile StopCapture();
    void SetAudioInputSource(string? sourceId);
    void ToggleDesktopAudio();
    void ToggleMute();
}
