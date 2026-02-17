using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domain.Capture.Interfaces;

public interface IAudioRecorder
{
    void Pause();
    void StartCapture();
    IAudioFile StopCapture();
    void ToggleDesktopAudio();
    void ToggleMute();
}