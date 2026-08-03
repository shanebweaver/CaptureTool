using CaptureKit.Windows.DependencyInjection;
using CaptureTool.Application.Abstractions.Capture;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;

public static class WindowsCaptureInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsCaptureDomains(this IServiceCollection services)
    {
        services.AddCaptureKitWindows();
        services.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        services.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();
        services.AddSingleton<IVideoCaptureSupportService, WindowsVideoCaptureSupportService>();
        services.AddSingleton<IAudioRecorder, WindowsAudioRecorder>();

        return services;
    }
}
