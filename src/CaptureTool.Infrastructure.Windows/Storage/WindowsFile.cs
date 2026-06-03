using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Infrastructure.Windows.Storage;

public partial class WindowsFile(string path) : IFile
{
    public string FilePath { get; } = path;
}