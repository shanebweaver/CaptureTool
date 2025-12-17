using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public partial class WindowsScreenRecorder : IScreenRecorder
{
    public bool StartRecording(IntPtr hMonitor, string outputPath, bool captureAudio = false)
        => CaptureInterop.TryStartRecording(hMonitor, outputPath, captureAudio);
    public void StopRecording() => CaptureInterop.TryStopRecording();

    public void PauseRecording() => CaptureInterop.TryPauseRecording();
    public void ResumeRecording() => CaptureInterop.TryResumeRecording();

    public void ToggleAudioCapture(bool enabled) => CaptureInterop.TryToggleAudioCapture(enabled);

}