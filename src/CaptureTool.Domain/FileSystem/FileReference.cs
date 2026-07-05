namespace CaptureTool.Domain.FileSystem;

public partial class FileReference
{
    public string FilePath { get; set; }

    public FileReference(string path)
    {
        FilePath = path;
    }
}
