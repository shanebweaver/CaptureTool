using CaptureTool.Application.Capture.Assets;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class CaptureAssetServiceCollectionExtensions
{
    public static IServiceCollection AddCaptureAssetServices(this IServiceCollection services)
    {
        services.AddSingleton<ICaptureAssetBootstrapper, CaptureAssetBootstrapper>();
        services.AddSingleton<ICaptureAssetLifecycleService, CaptureAssetLifecycleService>();
        return services;
    }
}
