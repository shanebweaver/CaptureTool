using CaptureTool.Domains.Edit.Implementations.Windows.ChromaKey;
using CaptureTool.Domains.Edit.Interfaces;
using CaptureTool.Domains.Edit.Interfaces.ChromaKey;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Domains.Edit.Implementations.Windows.DependencyInjection;

public static class EditDomainsWindowsServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsEditDomains(this IServiceCollection services)
    {
        services.AddSingleton<IChromaKeyService, Win2DChromaKeyService>();
        services.AddSingleton<IImageCanvasExporter, Win2DImageCanvasExporter>();
        services.AddSingleton<IImageCanvasPrinter, Win2DImageCanvasPrinter>();
        return services;
    }
}
