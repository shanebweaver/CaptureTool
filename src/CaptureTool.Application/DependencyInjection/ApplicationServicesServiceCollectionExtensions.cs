using CaptureTool.Application.Services.Activation;
using CaptureTool.Application.Services.Navigation;
using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Activation;
using Microsoft.Extensions.DependencyInjection;

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
