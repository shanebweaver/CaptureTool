using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Domains.Capture.Interfaces;

public abstract partial class FileBase : IFile
{
    public string FilePath { get; set; }

    public FileBase(string path)
    {
        FilePath = path;
    }
}
