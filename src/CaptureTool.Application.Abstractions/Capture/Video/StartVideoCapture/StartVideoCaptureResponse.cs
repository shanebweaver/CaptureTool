namespace CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;

public sealed record StartVideoCaptureResponse(
    bool Succeeded = true,
    StartVideoCaptureFailureReason FailureReason = StartVideoCaptureFailureReason.None);

public enum StartVideoCaptureFailureReason
{
    None = 0,
    NotSupported = 1
}
