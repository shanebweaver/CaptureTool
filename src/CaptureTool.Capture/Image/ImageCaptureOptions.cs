namespace CaptureTool.Capture.Image;

public sealed partial class ImageCaptureOptions : CaptureOptions
{
    public override CaptureMode CaptureMode { get; } = CaptureMode.Image;

    public ImageCaptureMode ImageCaptureMode { get; }

    public ImageFileType ImageFileType { get; }

    public ImageCaptureOptions(ImageCaptureMode captureMode, ImageFileType fileType, bool autoSave)
        : base(autoSave)
    {
        ImageCaptureMode = captureMode;
        ImageFileType = fileType;
    }
}
