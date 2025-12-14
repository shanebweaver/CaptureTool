using CaptureTool.Services.Interfaces.FeatureManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Services.Implementations.FeatureManagement.DependencyInjection;

public static class FeatureManagementServiceCollectionExtensions
{
    public static IServiceCollection AddFeatureManagementServices(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureManager, MicrosoftFeatureManager>();
        return services;
    }
}
