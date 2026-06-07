using Microsoft.Win32.SafeHandles;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal sealed class CaptureRecorderSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private CaptureRecorderSafeHandle()
        : base(ownsHandle: true)
    {
    }

    internal CaptureRecorderSafeHandle(IntPtr handle, bool ownsHandle)
        : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
        => CaptureV2NativeMethods.CtCaptureV2_DestroyRecorder(handle) == (int)CaptureV2ResultCode.Success;
}
