using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal static partial class CaptureV2NativeMethods
{
    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_GetApiVersion(out CaptureV2ApiVersion outVersion);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_CreateRecorder(out CaptureRecorderSafeHandle outHandle);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_DestroyRecorder(IntPtr handle);
}
