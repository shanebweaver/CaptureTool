using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Domain.Capture;

public abstract partial class FileBase : IFile
{
    public string FilePath { get; set; }

    public FileBase(string path)
    {
        FilePath = path;
    }
}
