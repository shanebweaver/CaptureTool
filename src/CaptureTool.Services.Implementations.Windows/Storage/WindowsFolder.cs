using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Services.Implementations.Windows.Storage;

public partial class WindowsFolder(string path) : IFolder
{
    public string FolderPath { get; } = path;
}