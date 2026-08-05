namespace CaptureTool.Application.Abstractions.Storage;

public partial interface IStorageService
{
    string GetApplicationDataFolderPath();
    string GetApplicationRetainedCaptureFolderPath();
    string GetApplicationScratchFolderPath();
    string GetSystemDefaultScreenshotsFolderPath();
    string GetTemporaryFileName();
    string GetSystemDefaultVideosFolderPath();
    string GetSystemDefaultMusicFolderPath();
}
