namespace CaptureTool.Services.Interfaces.Storage;

public partial interface IStorageService
{
    string GetSystemDefaultScreenshotsFolderPath();
    string GetApplicationTemporaryFolderPath();
    string GetTemporaryFileName();
}
