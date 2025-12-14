using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    public bool StartRecording(IntPtr hMonitor, string outputPath, bool enableAudio)
    {
        return CaptureInterop.TryStartRecording(hMonitor, outputPath, enableAudio);
    }

    public void PauseRecording() => CaptureInterop.TryPauseRecording();

    public void StopRecording() => CaptureInterop.TryStopRecording();
}