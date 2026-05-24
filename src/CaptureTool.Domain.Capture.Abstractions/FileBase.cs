using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Domain.Capture.Abstractions;

public abstract partial class FileBase : IFile
{
    public string FilePath { get; set; }

    public FileBase(string path)
    {
        FilePath = path;
    }
}
