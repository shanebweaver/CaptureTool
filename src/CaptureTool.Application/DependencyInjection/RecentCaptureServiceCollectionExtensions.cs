using CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Features.RecentCaptures;
using CaptureTool.Application.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class RecentCaptureServiceCollectionExtensions
{
    public static IServiceCollection AddRecentCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileTypeDetector, FileTypeDetector>();
        services.AddTransient<IGetRecentCapturesUseCase, GetRecentCapturesUseCase>();
        services.AddTransient<IOpenRecentCaptureUseCase, OpenRecentCaptureUseCase>();

        return services;
    }
}
