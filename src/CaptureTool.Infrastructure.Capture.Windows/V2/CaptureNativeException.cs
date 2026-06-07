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
        : base(message)
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
