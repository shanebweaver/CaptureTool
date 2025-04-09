namespace CaptureTool.Capture.Desktop;

public sealed partial class ImageFile
{
    public string Path { get; set; }

    public ImageFile(string path)
    {
        Path = path;
    }
}
