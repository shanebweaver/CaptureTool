namespace CaptureTool.Domain.Files;

public abstract partial class FileBase : IFile
{
    public string FilePath { get; set; }

    public FileBase(string path)
    {
        FilePath = path;
    }
}
