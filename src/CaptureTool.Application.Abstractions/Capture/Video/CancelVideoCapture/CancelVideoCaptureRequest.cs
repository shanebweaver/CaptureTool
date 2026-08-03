namespace CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;

public sealed record CancelVideoCaptureRequest(
    bool SkipConfirmation = false,
    CancelVideoCaptureReason Reason = CancelVideoCaptureReason.User);
