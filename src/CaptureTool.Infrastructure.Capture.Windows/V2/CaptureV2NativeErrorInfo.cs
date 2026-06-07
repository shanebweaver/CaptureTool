using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureV2NativeErrorInfo
{
    public uint Size;
    public uint Version;
    public int ResultCode;
    public int ErrorCode;
    public int NativeStatus;
    public int Stage;
    public nint Component;
    public nint Operation;
}
