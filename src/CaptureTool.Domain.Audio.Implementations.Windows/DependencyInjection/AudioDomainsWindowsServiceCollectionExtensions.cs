using CaptureTool.Domain.Audio.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Domain.Audio.Implementations.Windows.DependencyInjection;

public static class AudioDomainsWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsAudioDomains(this IServiceCollection services)
    {
        services.AddSingleton<IAudioCaptureService, WindowsAudioCaptureService>();
        services.AddSingleton<IAudioInputService, WindowsAudioInputService>();

        return services;
    }
}
