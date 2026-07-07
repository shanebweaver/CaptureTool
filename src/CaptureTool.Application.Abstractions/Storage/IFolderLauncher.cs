namespace CaptureTool.Application.Abstractions.Storage;

public interface IFolderLauncher
{
    bool TryOpenFolder(string folderPath);
}
