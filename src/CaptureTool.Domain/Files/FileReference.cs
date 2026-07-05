namespace CaptureTool.Domain.Files;

public partial class FileReference
{
    public string FilePath { get; set; }

    public FileReference(string path)
    {
        FilePath = path;
    }
}
