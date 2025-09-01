using System;
using System.Diagnostics;

namespace CaptureTool.Capture.Windows;

public static partial class ScreenRecorder
{
    public static bool StartRecording(IntPtr hMonitor, string outputPath)
    {
        bool success = CaptureInterop.TryStartRecording(hMonitor, outputPath);
        Debug.WriteLine(success);
        return success;
    }

    public static void PauseRecording() => CaptureInterop.TryPauseRecording();

    public static void StopRecording() => CaptureInterop.TryStopRecording();
}