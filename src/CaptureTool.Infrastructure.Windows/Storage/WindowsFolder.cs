using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Infrastructure.Windows.Storage;

public partial class WindowsFolder(string path) : IFolder
{
    public string FolderPath { get; } = path;
}