namespace CaptureTool.Domains.Capture.Implementations.Windows;

public static partial class ScreenRecorder
{
    public static bool StartRecording(IntPtr hMonitor, string outputPath)
    {
        return CaptureInterop.TryStartRecording(hMonitor, outputPath);
    }

    public static void PauseRecording() => CaptureInterop.TryPauseRecording();

    public static void StopRecording() => CaptureInterop.TryStopRecording();
}