using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class VideoServiceCollectionExtensions
{
    public static IServiceCollection AddVideoCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<IVideoCaptureHandler, CaptureToolVideoCaptureHandler>();

        return services;
    }

    public static IServiceCollection AddVideoEditUseCases(this IServiceCollection services)
    {
        services.AddTransient<ICopyVideoFileUseCase, CopyVideoFileUseCase>();
        services.AddTransient<ISaveVideoFileUseCase, SaveVideoFileUseCase>();
        services.AddTransient<IOpenVideoEditPageUseCase, OpenVideoEditPageUseCase>();

        return services;
    }
}
