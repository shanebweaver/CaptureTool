using CaptureTool.Services.Interfaces.Storage;
using Microsoft.Windows.Storage;

namespace CaptureTool.Services.Implementations.Windows.Storage;

public sealed partial class WindowsStorageService : IStorageService
{
    public string GetApplicationTemporaryFolderPath()
    {
        return ApplicationData.GetDefault().TemporaryPath;
    }

    public string GetSystemDefaultScreenshotsFolderPath()
    {
        return global::Windows.Storage.KnownFolders.SavedPictures.Path;
    }

    public string GetSystemTemporaryFolderPath()
    {
        return Path.GetTempPath();
    }

    public string GetTemporaryFileName()
    {
        return Path.GetTempFileName();
    }
}
