using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal static class CaptureV2ErrorReader
{
    public static CaptureV2ErrorDetails GetLastError(CaptureRecorderSafeHandle handle, CaptureV2ResultCode fallbackResultCode)
    {
        int sizingResult = CaptureV2NativeMethods.CtCaptureV2_GetLastError(
            handle,
            out CaptureV2NativeErrorInfo errorInfo,
            IntPtr.Zero,
            0,
            out uint requiredLength);

        if (sizingResult is not ((int)CaptureV2ResultCode.BufferTooSmall or (int)CaptureV2ResultCode.Success))
        {
            return CreateFallback(fallbackResultCode, $"Native error details could not be retrieved: {sizingResult}.");
        }

        if (requiredLength == 0)
        {
            return FromNative(errorInfo, string.Empty, fallbackResultCode);
        }

        IntPtr messageBuffer = Marshal.AllocHGlobal(checked((int)requiredLength * sizeof(char)));
        try
        {
            int readResult = CaptureV2NativeMethods.CtCaptureV2_GetLastError(
                handle,
                out errorInfo,
                messageBuffer,
                requiredLength,
                out _);

            if (readResult != (int)CaptureV2ResultCode.Success)
            {
                return CreateFallback(fallbackResultCode, $"Native error message could not be retrieved: {readResult}.");
            }

            string message = Marshal.PtrToStringUni(messageBuffer) ?? string.Empty;
            return FromNative(errorInfo, message, fallbackResultCode);
        }
        finally
        {
            Marshal.FreeHGlobal(messageBuffer);
        }
    }

    private static CaptureV2ErrorDetails FromNative(
        CaptureV2NativeErrorInfo errorInfo,
        string message,
        CaptureV2ResultCode fallbackResultCode)
    {
        CaptureV2ResultCode resultCode = errorInfo.ResultCode == (int)CaptureV2ResultCode.Success
            ? fallbackResultCode
            : (CaptureV2ResultCode)errorInfo.ResultCode;

        return new CaptureV2ErrorDetails(
            resultCode,
            errorInfo.ErrorCode,
            errorInfo.NativeStatus,
            errorInfo.Stage,
            Marshal.PtrToStringAnsi(errorInfo.Component) ?? string.Empty,
            Marshal.PtrToStringAnsi(errorInfo.Operation) ?? string.Empty,
            string.IsNullOrWhiteSpace(message) ? resultCode.ToString() : message);
    }

    private static CaptureV2ErrorDetails CreateFallback(CaptureV2ResultCode resultCode, string message)
        => new(
            resultCode,
            ErrorCode: (int)resultCode,
            NativeStatus: 0,
            Stage: 0,
            Component: "CaptureInteropV2",
            Operation: string.Empty,
            Message: message);
}
