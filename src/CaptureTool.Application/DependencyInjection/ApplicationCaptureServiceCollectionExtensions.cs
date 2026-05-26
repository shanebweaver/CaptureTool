using CaptureTool.Application.Abstractions.AudioCapture;
using CaptureTool.Application.Abstractions.ImageCapture;
using CaptureTool.Application.Abstractions.VideoCapture;
using CaptureTool.Application.AudioCapture;
using CaptureTool.Application.ImageCapture;
using CaptureTool.Application.VideoCapture;
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
