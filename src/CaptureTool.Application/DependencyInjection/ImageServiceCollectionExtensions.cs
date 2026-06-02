using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Features.ImageCapture;
using CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class ImageServiceCollectionExtensions
{
    public static IServiceCollection AddImageCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageCaptureHandler, CaptureToolImageCaptureHandler>();

        return services;
    }

    public static IServiceCollection AddImageEditUseCases(this IServiceCollection services)
    {
        services.AddTransient<IOpenImageEditPageUseCase, OpenImageEditPageUseCase>();

        return services;
    }
}
