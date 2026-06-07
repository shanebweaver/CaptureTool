using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Capture.Windows.V2;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;

public static class WindowsCaptureInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsCaptureDomains(
        this IServiceCollection services,
        Action<WindowsCaptureInfrastructureOptions>? configure = null)
    {
        var options = new WindowsCaptureInfrastructureOptions();
        configure?.Invoke(options);

        services.AddSingleton<IScreenCapture, WindowsScreenCapture>();
        AddScreenRecorder(services, options);
        services.AddSingleton<IAudioRecorder, WindowsAudioRecorder>();

        return services;
    }

    private static void AddScreenRecorder(
        IServiceCollection services,
        WindowsCaptureInfrastructureOptions options)
    {
        if (!options.UseCaptureV2ScreenRecorder)
        {
            services.AddSingleton<IScreenRecorder, WindowsScreenRecorder>();
            return;
        }

        if (options.CaptureV2ScreenRecorderFactory is not null)
        {
            services.AddSingleton(options.CaptureV2ScreenRecorderFactory);
            return;
        }

        services.AddSingleton<IScreenRecorder, CaptureV2ScreenRecorderAdapter>();
    }
}
