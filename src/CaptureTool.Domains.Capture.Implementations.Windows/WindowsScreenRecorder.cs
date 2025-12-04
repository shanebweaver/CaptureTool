using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    public bool StartRecording(IntPtr hMonitor, string outputPath)
    {
        return CaptureInterop.TryStartRecording(hMonitor, outputPath);
    }

    public void PauseRecording() => CaptureInterop.TryPauseRecording();

    public void StopRecording() => CaptureInterop.TryStopRecording();
}