namespace CaptureTool.Services.SnippingTool;

public sealed partial class SnippingToolCaptureOptions
{
    public SnippingToolCaptureMode CaptureMode { get; }

    public bool AutoSave { get; }

    public SnippingToolCaptureOptions(SnippingToolCaptureMode captureMode, bool autoSave)
    {
        CaptureMode = captureMode;
        AutoSave = autoSave;
    }
}
