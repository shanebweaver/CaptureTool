using CaptureTool.Application.Implementations.Services.Activation;
using CaptureTool.Application.Implementations.Services.Navigation;
using CaptureTool.Application.Interfaces;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Infrastructure.Interfaces.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.Implementations.DependencyInjection;

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
