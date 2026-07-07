using System.Runtime.InteropServices;

namespace CaptureTool.Application.Abstractions.Capture;

[StructLayout(LayoutKind.Sequential)]
public struct VideoFrameData
{
    public IntPtr pTexture;
    public long Timestamp;
    public uint Width;
    public uint Height;
}
