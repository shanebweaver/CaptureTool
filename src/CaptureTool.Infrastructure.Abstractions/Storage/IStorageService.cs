namespace CaptureTool.Infrastructure.Abstractions.Storage;

public partial interface IStorageService
{
    string GetSystemDefaultScreenshotsFolderPath();
    string GetApplicationTemporaryFolderPath();
    string GetTemporaryFileName();
    string GetSystemDefaultVideosFolderPath();
}
