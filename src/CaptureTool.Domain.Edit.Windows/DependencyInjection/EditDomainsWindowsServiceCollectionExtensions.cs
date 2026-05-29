using CaptureTool.Domain.Edit.Abstractions;
using CaptureTool.Domain.Edit.Abstractions.ChromaKey;
using CaptureTool.Domain.Edit.Windows.ChromaKey;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Domain.Edit.Windows.DependencyInjection;

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
