using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Domain.Capture.Abstractions;

public abstract partial class FileBase : IFile
{
    public string FilePath { get; set; }

    public FileBase(string path)
    {
        FilePath = path;
    }
}
