namespace CaptureTool.Infrastructure.Interfaces.Storage;

public partial interface IStorageService
{
    string GetSystemDefaultScreenshotsFolderPath();
    string GetApplicationTemporaryFolderPath();
    string GetTemporaryFileName();
    string GetSystemDefaultVideosFolderPath();
}
