using CaptureTool.Domains.Capture.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Domains.Capture.Implementations.Windows.DependencyInjection;

public static class CaptureDomainsWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsCaptureDomains(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        services.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();
        return services;
    }
}
