using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.OpenRecentCapture;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class RecentCaptureServiceCollectionExtensions
{
    public static IServiceCollection AddRecentCaptureServices(this IServiceCollection services)
    {
        services.AddTransient<IGetRecentCapturesUseCase, GetRecentCapturesUseCase>();
        services.AddTransient<IOpenRecentCaptureUseCase, OpenRecentCaptureUseCase>();

        return services;
    }
}
