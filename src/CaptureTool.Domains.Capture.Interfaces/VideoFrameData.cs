using System.Runtime.InteropServices;

namespace CaptureTool.Domains.Capture.Interfaces;

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

