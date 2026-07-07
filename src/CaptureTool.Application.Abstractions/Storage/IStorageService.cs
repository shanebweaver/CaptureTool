namespace CaptureTool.Application.Abstractions.Storage;

public partial interface IStorageService
{
    string GetApplicationDataFolderPath();
    string GetSystemDefaultScreenshotsFolderPath();
    string GetApplicationTemporaryFolderPath();
    string GetTemporaryFileName();
    string GetSystemDefaultVideosFolderPath();
}
