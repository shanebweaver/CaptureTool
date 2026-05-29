using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Domain.Capture.Abstractions;

public interface IAudioRecorder
{
    void Pause();
    void StartCapture();
    IAudioFile StopCapture();
    void ToggleDesktopAudio();
    void ToggleMute();
}