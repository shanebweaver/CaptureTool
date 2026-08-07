using CaptureTool.Application.Abstractions.Storage;
using Microsoft.Windows.Storage;

namespace CaptureTool.Infrastructure.Windows.Storage;

internal sealed class WindowsApplicationLocalCachePathProvider : IApplicationLocalCachePathProvider
{
    public string GetApplicationLocalCacheFolderPath()
    {
        return ApplicationData.GetDefault().LocalCachePath;
    }
}
