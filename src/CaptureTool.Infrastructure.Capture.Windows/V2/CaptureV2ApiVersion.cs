using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2ApiVersion
{
    public uint Size;
    public uint Version;
    public uint Major;
    public uint Minor;
    public uint Patch;
    public uint Reserved;
}
