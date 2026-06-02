namespace CaptureTool.Application.Abstractions.Storage;

public partial interface IStorageService
{
    string GetSystemDefaultScreenshotsFolderPath();
    string GetApplicationTemporaryFolderPath();
    string GetTemporaryFileName();
    string GetSystemDefaultVideosFolderPath();
}
