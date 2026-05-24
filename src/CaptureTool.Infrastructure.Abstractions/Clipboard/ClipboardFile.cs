namespace CaptureTool.Infrastructure.Abstractions.Clipboard;

public readonly struct ClipboardFile
{
    public string FilePath { get; }

    public ClipboardFile(string filePath)
    {
        FilePath = filePath;
    }
}