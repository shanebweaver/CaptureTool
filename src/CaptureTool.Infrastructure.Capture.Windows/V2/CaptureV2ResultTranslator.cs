namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal static class CaptureV2ResultTranslator
{
    public static void ThrowIfFailed(CaptureRecorderSafeHandle handle, int resultCode)
    {
        if (resultCode == (int)CaptureV2ResultCode.Success)
        {
            return;
        }

        CaptureV2ResultCode managedResultCode = (CaptureV2ResultCode)resultCode;
        throw CreateException(CaptureV2ErrorReader.GetLastError(handle, managedResultCode));
    }

    public static CaptureNativeException CreateException(CaptureV2ErrorDetails details)
        => IsValidationFailure(details.ResultCode)
            ? new CaptureValidationException(
                details.ResultCode,
                details.NativeStatus,
                details.Component,
                details.Operation,
                details.Stage,
                details.Message)
            : new CaptureNativeException(
                details.ResultCode,
                details.NativeStatus,
                details.Component,
                details.Operation,
                details.Stage,
                details.Message);

    private static bool IsValidationFailure(CaptureV2ResultCode resultCode)
        => resultCode is CaptureV2ResultCode.ValidationFailed or CaptureV2ResultCode.UnsupportedVersion;
}
