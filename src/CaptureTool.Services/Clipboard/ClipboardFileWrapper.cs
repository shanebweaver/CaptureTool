namespace CaptureTool.Services.Clipboard;

public class ClipboardFileWrapper : IClipboardFile
{
    public string FilePath { get; }

    public ClipboardFileWrapper(string filePath)
    {
        FilePath = filePath;
    }
}