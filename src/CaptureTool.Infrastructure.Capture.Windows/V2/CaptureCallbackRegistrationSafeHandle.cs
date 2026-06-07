using Microsoft.Win32.SafeHandles;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal sealed class CaptureCallbackRegistrationSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly CaptureV2NativeMethods.CaptureV2NativeEventCallback _callback;

    internal CaptureCallbackRegistrationSafeHandle(
        nint handle,
        CaptureV2NativeMethods.CaptureV2NativeEventCallback callback)
        : base(ownsHandle: true)
    {
        _callback = callback;
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        bool released = CaptureV2NativeMethods.CtCaptureV2_UnregisterCallbacks(handle) == (int)CaptureV2ResultCode.Success;
        GC.KeepAlive(_callback);
        return released;
    }
}
