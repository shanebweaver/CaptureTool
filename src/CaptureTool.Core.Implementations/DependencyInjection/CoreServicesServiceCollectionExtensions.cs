using CaptureTool.Core.Implementations.Services.Activation;
using CaptureTool.Core.Implementations.Services.Navigation;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Activation;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Core.Implementations.DependencyInjection;

public static class CoreServicesServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Core app services
        services.AddSingleton<IActivationHandler, CaptureToolActivationHandler>();
        services.AddSingleton<IAppNavigation, CaptureToolAppNavigation>();
        return services;
    }
}
