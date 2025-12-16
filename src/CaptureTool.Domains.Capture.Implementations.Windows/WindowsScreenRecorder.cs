using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    public bool StartRecording(IntPtr hMonitor, string outputPath, bool captureAudio = false)
    {
        return CaptureInterop.TryStartRecording(hMonitor, outputPath, captureAudio);
    }

    public void PauseRecording() => CaptureInterop.TryPauseRecording();

    public void StopRecording() => CaptureInterop.TryStopRecording();
}