namespace CaptureTool.Capture.Desktop;

public abstract class DesktopCaptureOptions
{
    public abstract DesktopCaptureMode CaptureMode { get; }

    public bool AutoSave { get; }

    protected DesktopCaptureOptions(bool autoSave)
    {
        AutoSave = autoSave;
    }
}
