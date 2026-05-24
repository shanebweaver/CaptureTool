using CaptureTool.Application.Capture;
using CaptureTool.Domain.Capture.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationCaptureServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageCaptureHandler, CaptureToolImageCaptureHandler>();
        services.AddSingleton<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();
        services.AddSingleton<IAudioCaptureHandler, CaptureToolAudioCaptureHandler>();
        return services;
    }
}
