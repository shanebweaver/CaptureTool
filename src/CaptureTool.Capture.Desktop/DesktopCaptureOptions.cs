namespace CaptureTool.Capture.Desktop;

public sealed partial class DesktopCaptureOptions
{
    public DesktopCaptureMode CaptureMode { get; }

    public DesktopImageCaptureMode? ImageCaptureMode { get; }

    public DesktopVideoCaptureMode? VideoCaptureMode { get; }

    public bool AutoSave { get; }

    public ImageFileType? ImageFileType { get; }

    public VideoFileType? VideoFileType { get; }

    public DesktopCaptureOptions(DesktopImageCaptureMode captureMode, ImageFileType fileType, bool autoSave)
    {
        CaptureMode = DesktopCaptureMode.Image;
        ImageCaptureMode = captureMode;
        ImageFileType = fileType;
        AutoSave = autoSave;
    }

    public DesktopCaptureOptions(DesktopVideoCaptureMode captureMode, VideoFileType fileType, bool autoSave)
    {
        CaptureMode = DesktopCaptureMode.Video;
        VideoCaptureMode = captureMode;
        VideoFileType = fileType;
        AutoSave = autoSave;
    }
}
