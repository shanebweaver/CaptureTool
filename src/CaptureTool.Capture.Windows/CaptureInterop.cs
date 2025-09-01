using System;
using System.Runtime.InteropServices;

namespace CaptureTool.Capture.Windows;

internal static partial class CaptureInterop
{
    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode)]
    internal static extern bool TryStartRecording(IntPtr hMonitor, string outputPath);

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryPauseRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryStopRecording();
}