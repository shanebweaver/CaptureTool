namespace CaptureTool.Capture.Desktop;

public sealed partial class DesktopVideoCaptureOptions : DesktopCaptureOptions
{
    public override DesktopCaptureMode CaptureMode { get; } = DesktopCaptureMode.Video;

    public DesktopVideoCaptureMode VideoCaptureMode { get; }

    public VideoFileType VideoFileType { get; }

    public DesktopVideoCaptureOptions(DesktopVideoCaptureMode captureMode, VideoFileType fileType, bool autoSave)
        : base(autoSave)
    {
        VideoCaptureMode = captureMode;
        VideoFileType = fileType;
    }
}
