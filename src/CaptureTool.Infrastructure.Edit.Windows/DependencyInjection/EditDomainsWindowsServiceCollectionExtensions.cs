using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.ChromaKey;
using CaptureTool.Infrastructure.Edit.Windows.ChromaKey;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Edit.Windows.DependencyInjection;

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
