using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Activation;
using Microsoft.Extensions.DependencyInjection;
using CaptureTool.Application.Activation;
using CaptureTool.Application.Navigation;
using CaptureTool.Application.Abstractions.RecentCaptures;
using CaptureTool.Application.RecentCaptures;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationServicesServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Core app services
        services.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();
        services.AddSingleton<IAppNavigation, CaptureToolAppNavigation>();
        services.AddSingleton<IFileTypeDetector, FileTypeDetector>();
        return services;
    }
}
