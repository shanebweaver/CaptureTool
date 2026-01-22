namespace CaptureTool.Domain.Capture.Interfaces;

public readonly partial struct CaptureOptions
{
    public static CaptureOptions ImageDefault => new(CaptureMode.Image, CaptureType.Rectangle);
    public static CaptureOptions VideoDefault => new(CaptureMode.Video, CaptureType.FullScreen);

    public CaptureMode CaptureMode { get; }
    public CaptureType CaptureType { get; }

    public CaptureOptions(CaptureMode captureMode, CaptureType captureType)
    {
        CaptureMode = captureMode;
        CaptureType = captureType;
    }
}
