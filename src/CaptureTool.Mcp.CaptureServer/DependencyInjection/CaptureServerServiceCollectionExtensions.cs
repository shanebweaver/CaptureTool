using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Annotations;
using CaptureTool.Mcp.CaptureServer.Capture;
using CaptureTool.Mcp.CaptureServer.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Mcp.CaptureServer.DependencyInjection;

internal static class CaptureServerServiceCollectionExtensions
{
    public static IServiceCollection AddCaptureServerServices(this IServiceCollection services)
        => services
            .AddSingleton<IMcpCaptureStore, InMemoryMcpCaptureStore>()
            .AddSingleton<ICaptureKitImageCaptureAdapter, CaptureKitImageCaptureAdapter>()
            .AddSingleton<IPrimaryMonitorCaptureService, PrimaryMonitorCaptureService>()
            .AddSingleton<IRegionCaptureService, RegionCaptureService>()
            .AddSingleton<IAllScreensCaptureService, AllScreensCaptureService>()
            .AddSingleton<IWindowCaptureService, WindowCaptureService>()
            .AddSingleton<AnnotationDrawableFactory>()
            .AddSingleton<IAnnotationService, AnnotationService>()
            .AddSingleton(TimeProvider.System);
}
