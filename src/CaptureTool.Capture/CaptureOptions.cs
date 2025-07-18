namespace CaptureTool.Capture;

public readonly partial struct CaptureOptions
{
    public CaptureMode CaptureMode { get; }
    public CaptureType CaptureType { get; }
    public bool AutoSave { get; }

    public CaptureOptions(CaptureMode captureMode, CaptureType captureType, bool autoSave)
    {
        CaptureMode = captureMode;
        CaptureType = captureType;
        AutoSave = autoSave;
    }
}
