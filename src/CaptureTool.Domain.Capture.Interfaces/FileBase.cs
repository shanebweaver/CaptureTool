using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Domain.Capture.Interfaces;

public abstract partial class FileBase : IFile
{
    public string FilePath { get; set; }

    public FileBase(string path)
    {
        FilePath = path;
    }
}
