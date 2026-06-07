namespace CaptureTool.Infrastructure.Capture.Windows.V2;

public class CaptureNativeException : Exception
{
    public CaptureNativeException(
        CaptureV2ResultCode resultCode,
        int nativeStatus,
        string component,
        string operation,
        int stage,
        string message)
        : base(FormatMessage(message, component, operation, nativeStatus, stage))
    {
        ResultCode = resultCode;
        NativeStatus = nativeStatus;
        Component = component;
        Operation = operation;
        Stage = stage;
    }

    public CaptureV2ResultCode ResultCode { get; }

    public int NativeStatus { get; }

    public string Component { get; }

    public string Operation { get; }

    public int Stage { get; }

    private static string FormatMessage(
        string message,
        string component,
        string operation,
        int nativeStatus,
        int stage)
    {
        string nativeStatusText = nativeStatus == 0
            ? "0"
            : $"0x{nativeStatus:X8}";

        return $"{message} Component={component}; Operation={operation}; NativeStatus={nativeStatusText}; Stage={stage}.";
    }
}

public sealed class CaptureValidationException : CaptureNativeException
{
    public CaptureValidationException(
        CaptureV2ResultCode resultCode,
        int nativeStatus,
        string component,
        string operation,
        int stage,
        string message)
        : base(resultCode, nativeStatus, component, operation, stage, message)
    {
    }
}
