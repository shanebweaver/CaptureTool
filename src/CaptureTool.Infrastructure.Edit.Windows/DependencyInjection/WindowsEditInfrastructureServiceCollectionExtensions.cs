using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Infrastructure.Edit.Windows.ChromaKey;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Edit.Windows.DependencyInjection;

public static class WindowsEditInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsEditDomains(this IServiceCollection services)
    {
        services.AddSingleton<IChromaKeyService, Win2DChromaKeyService>();
        services.AddSingleton<IImageSuperResolutionService, WindowsImageSuperResolutionService>();
        services.AddSingleton<ITextExtractionService, WindowsTextExtractionService>();
        services.AddSingleton<IImageCanvasExporter, Win2DImageCanvasExporter>();
        services.AddSingleton<IImageCanvasPrinter, Win2DImageCanvasPrinter>();
        return services;
    }
}
