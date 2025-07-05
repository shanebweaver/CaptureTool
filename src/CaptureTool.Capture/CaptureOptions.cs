namespace CaptureTool.Capture;

public abstract class CaptureOptions
{
    public bool AutoSave { get; }

    protected CaptureOptions(bool autoSave)
    {
        AutoSave = autoSave;
    }
}
