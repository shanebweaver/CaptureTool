namespace CaptureTool.Capture;

public readonly partial struct CaptureOptions
{
    public CaptureMode CaptureMode { get; }
    public CaptureType CaptureType { get; }

    public CaptureOptions(CaptureMode captureMode, CaptureType captureType)
    {
        CaptureMode = captureMode;
        CaptureType = captureType;
    }
}
