using CaptureTool.Application.Implementations.Capture;
using CaptureTool.Domain.Capture.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.Implementations.DependencyInjection;

public static class CoreCaptureServiceCollectionExtensions
{
    public static IServiceCollection AddCoreCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageCaptureHandler, CaptureToolImageCaptureHandler>();
        services.AddSingleton<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();
        return services;
    }
}
