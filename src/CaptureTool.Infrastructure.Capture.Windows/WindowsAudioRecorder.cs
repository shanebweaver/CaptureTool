using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Infrastructure.Capture.Windows;

public class WindowsAudioRecorder : IAudioRecorder
{
    public void Pause()
    {
        throw new NotImplementedException();
    }

    public void StartCapture()
    {
        throw new NotImplementedException();
    }

    public IAudioFile StopCapture()
    {
        throw new NotImplementedException();
    }

    public void ToggleDesktopAudio()
    {
        throw new NotImplementedException();
    }

    public void ToggleMute()
    {
        throw new NotImplementedException();
    }
}
