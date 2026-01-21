namespace CaptureTool.Infrastructure.Interfaces.Clipboard;

public readonly struct ClipboardFile
{
    public string FilePath { get; }

    public ClipboardFile(string filePath)
    {
        FilePath = filePath;
    }
}