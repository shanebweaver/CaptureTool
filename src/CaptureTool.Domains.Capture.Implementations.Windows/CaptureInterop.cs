using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

internal static partial class CaptureInterop
{
    // Legacy 3-parameter version (for backward compatibility)
    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode)]
    internal static extern bool TryStartRecording(IntPtr hMonitor, string outputPath, bool captureAudio = false);
    
    // New 4-parameter version with microphone support
    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode, EntryPoint = "TryStartRecording")]
    internal static extern bool TryStartRecordingWithMicrophone(IntPtr hMonitor, string outputPath, bool captureDesktopAudio, bool captureMicrophone);

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryPauseRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryResumeRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryStopRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryToggleAudioCapture(bool enabled);
}