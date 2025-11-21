using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Capture;

public abstract partial class FileBase : IFile
{
    public string FilePath { get; set; }

    public FileBase(string path)
    {
        FilePath = path;
    }
}
