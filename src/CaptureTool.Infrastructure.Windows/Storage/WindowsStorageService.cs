using CaptureTool.Application.Abstractions.Storage;
using Microsoft.Windows.Storage;

namespace CaptureTool.Infrastructure.Windows.Storage;

public sealed partial class WindowsStorageService : IStorageService
{
    public string GetApplicationTemporaryFolderPath()
    {
        return ApplicationData.GetDefault().TemporaryPath;
    }

    public string GetSystemDefaultScreenshotsFolderPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    public string GetSystemDefaultVideosFolderPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    }

    public string GetTemporaryFileName()
    {
        return $"{Guid.NewGuid()}.tmp";
    }
}
