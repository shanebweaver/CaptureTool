using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

internal static partial class CaptureInterop
{
    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode)]
    internal static extern bool TryStartRecording(IntPtr hMonitor, string outputPath, bool captureAudio);

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryPauseRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryResumeRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryStopRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryToggleAudioCapture(bool enabled);
}