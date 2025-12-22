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

    /// <summary>
    /// Set a callback to be invoked when a video frame is captured.
    /// </summary>
    /// <param name="callback">Callback to receive video frame data, or null to unregister.</param>
    public void SetVideoFrameCallback(VideoFrameCallback? callback)
        => CaptureInterop.SetVideoFrameCallback(callback);

    /// <summary>
    /// Set a callback to be invoked when an audio sample is captured.
    /// </summary>
    /// <param name="callback">Callback to receive audio sample data, or null to unregister.</param>
    public void SetAudioSampleCallback(AudioSampleCallback? callback)
        => CaptureInterop.SetAudioSampleCallback(callback);
}