using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Interfaces;

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

