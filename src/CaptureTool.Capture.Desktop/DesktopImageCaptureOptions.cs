namespace CaptureTool.Capture.Desktop;

public sealed partial class DesktopImageCaptureOptions : DesktopCaptureOptions
{
    public override DesktopCaptureMode CaptureMode { get; } = DesktopCaptureMode.Image;

    public DesktopImageCaptureMode ImageCaptureMode { get; }

    public ImageFileType ImageFileType { get; }

    public DesktopImageCaptureOptions(DesktopImageCaptureMode captureMode, ImageFileType fileType, bool autoSave)
        : base(autoSave)
    {
        ImageCaptureMode = captureMode;
        ImageFileType = fileType;
    }
}
