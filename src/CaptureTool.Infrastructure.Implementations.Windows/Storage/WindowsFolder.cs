using CaptureTool.Infrastructure.Interfaces.Storage;

namespace CaptureTool.Infrastructure.Implementations.Windows.Storage;

public partial class WindowsFolder(string path) : IFolder
{
    public string FolderPath { get; } = path;
}