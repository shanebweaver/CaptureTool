namespace CaptureTool.Application.Abstractions.Capture;

public interface IVideoCaptureSupportService
{
    VideoCaptureSupportStatus GetSupportStatus();
}

public readonly record struct VideoCaptureSupportStatus(
    bool IsSupported,
    VideoCaptureUnsupportedReason UnsupportedReason = VideoCaptureUnsupportedReason.None)
{
    public static VideoCaptureSupportStatus Supported { get; } = new(true);

    public static VideoCaptureSupportStatus Unsupported(VideoCaptureUnsupportedReason reason)
        => new(false, reason);
}

public enum VideoCaptureUnsupportedReason
{
    None = 0,
    OperatingSystem = 1,
    GraphicsCapture = 2
}

public sealed class VideoCaptureNotSupportedException : NotSupportedException
{
    public VideoCaptureNotSupportedException(VideoCaptureUnsupportedReason reason)
        : base("Video capture is not supported on this device.")
    {
        Reason = reason;
    }

    public VideoCaptureUnsupportedReason Reason { get; }
}

public sealed class VideoCaptureTargetUnavailableException : InvalidOperationException
{
    public VideoCaptureTargetUnavailableException()
        : base("The selected capture target is no longer available.")
    {
    }
}
