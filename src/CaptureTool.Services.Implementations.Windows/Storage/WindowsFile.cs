using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Services.Implementations.Windows.Storage;

public partial class WindowsFile(string path) : IFile
{
    public string FilePath { get; } = path;
}