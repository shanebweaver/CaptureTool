using System.Runtime.InteropServices;

namespace CaptureTool.Application.Abstractions.Capture;

[StructLayout(LayoutKind.Sequential)]
public struct AudioSampleData
{
    public IntPtr pData;
    public uint NumFrames;
    public long Timestamp;
    public uint SampleRate;
    public ushort Channels;
    public ushort BitsPerSample;
}
