using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Domain.Capture;

public interface IAudioRecorder
{
    void Pause();
    void StartCapture();
    IAudioFile StopCapture();
    void ToggleDesktopAudio();
    void ToggleMute();
}