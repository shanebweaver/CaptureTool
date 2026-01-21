using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Infrastructure.Implementations.Windows.Storage;

public partial class WindowsFile(string path) : IFile
{
    public string FilePath { get; } = path;
}