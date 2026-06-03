using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this ServiceCollection services)
    {
        services
            .AddActivationServices()
            .AddAppMenuUseCases()
            .AddAudioCaptureServices()
            .AddAudioEditUseCases()
            .AddCaptureOverlayUseCases()
            .AddDiagnosticsUseCases()
            .AddImageCaptureServices()
            .AddImageEditUseCases()
            .AddNavigationUseCases()
            .AddRecentCaptureServices()
            .AddSettingsUseCases()
            .AddStoreUseCases()
            .AddVideoCaptureServices()
            .AddVideoEditUseCases()
            .AddWindowingUseCases();

        return services;
    }
}
