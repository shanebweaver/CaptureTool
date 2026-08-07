using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Library.CaptureMemory;
using CaptureTool.Application.Library.RecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Library.RecentCaptures.RemoveRecentCapture;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

internal static class RecentCaptureServiceCollectionExtensions
{
    public static IServiceCollection AddRecentCaptureServices(this IServiceCollection services)
    {
        services.AddSingleton<IRecentCapturesChangeNotifier, RecentCapturesChangeNotifier>();
        services.AddTransient<IClearRecentCapturesUseCase, ClearRecentCapturesUseCase>();
        services.AddTransient<IGetRecentCapturesUseCase, GetRecentCapturesUseCase>();
        services.AddTransient<IOpenRecentCaptureUseCase, OpenRecentCaptureUseCase>();
        services.AddTransient<IRemoveRecentCaptureUseCase, RemoveRecentCaptureUseCase>();
        services.AddSingleton<ICaptureMemoryResultResolver, CaptureMemoryResultResolver>();
        services.AddTransient<IOpenCaptureMemoryResultUseCase, OpenCaptureMemoryResultUseCase>();

        return services;
    }
}
