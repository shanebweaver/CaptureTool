using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domain.Capture.Windows;

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
