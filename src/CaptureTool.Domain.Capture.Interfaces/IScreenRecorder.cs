using System.Runtime.InteropServices;

namespace CaptureTool.Domain.Capture.Interfaces;

public partial interface IScreenRecorder
{
    bool StartRecording(nint hMonitor, string outputPath, bool captureAudio = false);
    void StopRecording();
    void PauseRecording();
    void ResumeRecording();
    void ToggleAudioCapture(bool enabled);
    void SetVideoFrameCallback(VideoFrameCallback? callback);
    void SetAudioSampleCallback(AudioSampleCallback? callback);
}

/// <summary>
/// Delegate for video frame callback from native layer.
/// </summary>
/// <param name="frameData">Video frame data structure.</param>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate void VideoFrameCallback(ref VideoFrameData frameData);

/// <summary>
/// Delegate for audio sample callback from native layer.
/// </summary>
/// <param name="sampleData">Audio sample data structure.</param>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate void AudioSampleCallback(ref AudioSampleData sampleData);

