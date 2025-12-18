using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    // Legacy 3-parameter method (for backward compatibility)
    public bool StartRecording(IntPtr hMonitor, string outputPath, bool captureAudio = false)
        => CaptureInterop.TryStartRecording(hMonitor, outputPath, captureAudio);
    
    // New 4-parameter method with microphone support
    public bool StartRecording(IntPtr hMonitor, string outputPath, bool captureDesktopAudio, bool captureMicrophone)
        => CaptureInterop.TryStartRecordingWithMicrophone(hMonitor, outputPath, captureDesktopAudio, captureMicrophone);
    
    public void StopRecording() => CaptureInterop.TryStopRecording();

    public void PauseRecording() => CaptureInterop.TryPauseRecording();
    public void ResumeRecording() => CaptureInterop.TryResumeRecording();

    public void ToggleAudioCapture(bool enabled) => CaptureInterop.TryToggleAudioCapture(enabled);

}