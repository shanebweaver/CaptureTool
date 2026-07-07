using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Domain.Capture;
using CaptureKit.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;

public static class WindowsCaptureInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsCaptureDomains(this IServiceCollection services)
    {
        services.AddCaptureKitWindows();
        services.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        services.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();
        services.AddSingleton<IAudioRecorder, WindowsAudioRecorder>();

        return services;
    }
}
