namespace CaptureTool.Infrastructure.Capture.Windows.V2;

internal sealed record CaptureV2ErrorDetails(
    CaptureV2ResultCode ResultCode,
    int ErrorCode,
    int NativeStatus,
    int Stage,
    string Component,
    string Operation,
    string Message);
