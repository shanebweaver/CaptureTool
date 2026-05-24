using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Infrastructure.Windows.Storage;

public partial class WindowsFile(string path) : IFile
{
    public string FilePath { get; } = path;
}