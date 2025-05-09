namespace CaptureTool.Capture.Desktop;

public abstract partial class FileBase
{
    public string Path { get; set; }

    public FileBase(string path)
    {
        Path = path;
    }
}
