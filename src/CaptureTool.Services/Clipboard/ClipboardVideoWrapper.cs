namespace CaptureTool.Services.Clipboard;

public class ClipboardVideoWrapper : IClipboardVideo
{
    public string FilePath { get; }

    public ClipboardVideoWrapper(string filePath)
    {
        FilePath = filePath;
    }
}