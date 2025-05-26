namespace CaptureTool.Capture;

public abstract class CaptureOptions
{
    public abstract CaptureMode CaptureMode { get; }

    public bool AutoSave { get; }

    protected CaptureOptions(bool autoSave)
    {
        AutoSave = autoSave;
    }
}
