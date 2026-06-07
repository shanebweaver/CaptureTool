using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal static partial class CaptureV2NativeMethods
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void CaptureV2NativeEventCallback(nint eventData, nint userData);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_GetApiVersion(out CaptureV2ApiVersion outVersion);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_CreateRecorder(out CaptureRecorderSafeHandle outHandle);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_DestroyRecorder(IntPtr handle);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_Pause(CaptureRecorderSafeHandle handle);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_Start(
        CaptureRecorderSafeHandle handle,
        in CaptureV2NativeConfig config);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_Resume(CaptureRecorderSafeHandle handle);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_SetAudioMuted(
        CaptureRecorderSafeHandle handle,
        uint sourceId,
        byte muted);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_SetAudioGain(
        CaptureRecorderSafeHandle handle,
        uint sourceId,
        float gainDb);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_Stop(
        CaptureRecorderSafeHandle handle,
        out CaptureV2NativeStopResult result);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_GetLastError(
        CaptureRecorderSafeHandle handle,
        out CaptureV2NativeErrorInfo errorInfo,
        IntPtr messageBuffer,
        uint messageBufferLength,
        out uint requiredMessageLength);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_RegisterCallbacks(
        CaptureRecorderSafeHandle handle,
        in CaptureV2NativeCallbackConfig config,
        out nint outRegistration);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_UnregisterCallbacks(nint registration);

    [DllImport("CaptureInterop.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CtCaptureV2_TestTriggerEvent(
        CaptureRecorderSafeHandle handle,
        in CaptureV2NativeEvent eventData);
}
