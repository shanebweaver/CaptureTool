using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

/// <summary>
/// Data structure for video frame information passed from native layer.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VideoFrameData
{
    public IntPtr pTexture;         // Pointer to ID3D11Texture2D
    public long Timestamp;          // Timestamp in 100ns ticks
    public uint Width;              // Frame width in pixels
    public uint Height;             // Frame height in pixels
}

/// <summary>
/// Data structure for audio sample information passed from native layer.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AudioSampleData
{
    public IntPtr pData;            // Pointer to audio sample data
    public uint NumFrames;          // Number of audio frames
    public long Timestamp;          // Timestamp in 100ns ticks
    public uint SampleRate;         // Sample rate in Hz
    public ushort Channels;         // Number of channels
    public ushort BitsPerSample;    // Bits per sample
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

internal static partial class CaptureInterop
{
    [DllImport("CaptureInterop.dll", CharSet = CharSet.Unicode)]
    internal static extern bool TryStartRecording(IntPtr hMonitor, string outputPath, bool captureAudio = false);

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryPauseRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryResumeRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryStopRecording();

    [DllImport("CaptureInterop.dll")]
    internal static extern void TryToggleAudioCapture(bool enabled);

    [DllImport("CaptureInterop.dll")]
    internal static extern void SetVideoFrameCallback(VideoFrameCallback? callback);

    [DllImport("CaptureInterop.dll")]
    internal static extern void SetAudioSampleCallback(AudioSampleCallback? callback);
}