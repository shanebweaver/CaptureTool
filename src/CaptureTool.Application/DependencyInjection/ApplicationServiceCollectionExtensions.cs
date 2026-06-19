using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this ServiceCollection services)
    {
        services
            .AddUseCaseServices()
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

    private static IServiceCollection AddUseCaseServices(this IServiceCollection services)
    {
        services.AddTransient<IUseCaseExecutor, UseCaseExecutor>();
        services.AddSingleton<IActiveEditSessionService, ActiveEditSessionService>();
        services.AddSingleton<IEditSessionGuard, EditSessionGuard>();

        return services;
    }
}
