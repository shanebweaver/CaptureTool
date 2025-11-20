using CaptureTool.Services.Interfaces.Clipboard;

namespace CaptureTool.Services.Implementations.Clipboard;

public class ClipboardFileWrapper : IClipboardFile
{
    public string FilePath { get; }

    public ClipboardFileWrapper(string filePath)
    {
        FilePath = filePath;
    }
}