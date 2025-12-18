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

    // Audio Routing Configuration APIs (Phase 3)
    [DllImport("CaptureInterop.dll")]
    internal static extern void SetAudioSourceTrack(int sourceId, int trackIndex);

    [DllImport("CaptureInterop.dll")]
    internal static extern int GetAudioSourceTrack(int sourceId);

    [DllImport("CaptureInterop.dll")]
    internal static extern void SetAudioSourceVolume(int sourceId, float volume);

    [DllImport("CaptureInterop.dll")]
    internal static extern float GetAudioSourceVolume(int sourceId);

    [DllImport("CaptureInterop.dll")]
    internal static extern void SetAudioSourceMuted(int sourceId, bool muted);

    [DllImport("CaptureInterop.dll")]
    internal static extern bool IsAudioSourceMuted(int sourceId);

    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode)]
    internal static extern void SetAudioTrackName(int trackIndex, string trackName);

    [DllImport("CaptureInterop.dll")]
    internal static extern void SetAudioMixingMode(bool mixedMode);

    [DllImport("CaptureInterop.dll")]
    internal static extern bool GetAudioMixingMode();
}