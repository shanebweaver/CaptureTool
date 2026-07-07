using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureImage;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Capture.Image;
using CaptureTool.Application.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Capture.Image.CaptureImage;
using CaptureTool.Application.Edit.Image.ChromaKey;
using CaptureTool.Application.Edit.Image.OpenImageEditPage;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class ImageServiceCollectionExtensions
{
    public static IServiceCollection AddImageCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<ImageCaptureFileNameGenerator>();
        services.AddSingleton<ImageCapturePostProcessor>();
        services.AddSingleton<ImageCaptureWorkflow>();
        services.AddSingleton<IImageCaptureWorkflow>(provider => provider.GetRequiredService<ImageCaptureWorkflow>());
        services.AddSingleton<IImageCaptureState>(provider => provider.GetRequiredService<ImageCaptureWorkflow>());
        services.AddTransient<ICaptureAllScreensImageUseCase, CaptureAllScreensImageUseCase>();
        services.AddTransient<ICaptureImageUseCase, CaptureImageUseCase>();

        return services;
    }

    public static IServiceCollection AddImageEditUseCases(this IServiceCollection services)
    {
        services.AddTransient<IChromaKeyAccessService, ChromaKeyAccessService>();
        services.AddTransient<IOpenImageEditPageUseCase, OpenImageEditPageUseCase>();

        return services;
    }
}
