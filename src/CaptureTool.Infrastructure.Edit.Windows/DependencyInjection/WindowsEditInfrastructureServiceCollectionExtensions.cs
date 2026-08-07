using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Infrastructure.Edit.Windows.ChromaKey;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Edit.Windows.DependencyInjection;

public static class WindowsEditInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsEditDomains(this IServiceCollection services)
    {
        services.AddSingleton<IChromaKeyService, Win2DChromaKeyService>();
        services.AddSingleton<IImageSuperResolutionService, WindowsImageSuperResolutionService>();
        services.AddSingleton<WindowsTextExtractionService>();
        services.AddSingleton<ITextExtractionService>(provider =>
            provider.GetRequiredService<WindowsTextExtractionService>());
        services.AddSingleton<ITextExtractionAnalysisService>(provider =>
            provider.GetRequiredService<WindowsTextExtractionService>());
        services.AddSingleton<WindowsImageDescriptionService>();
        services.AddSingleton<IImageDescriptionService>(provider =>
            provider.GetRequiredService<WindowsImageDescriptionService>());
        services.AddSingleton<IImageDescriptionAnalysisService>(provider =>
            provider.GetRequiredService<WindowsImageDescriptionService>());
        services.AddSingleton<IImageForegroundExtractionService, WindowsImageForegroundExtractionService>();
        services.AddSingleton<IImageObjectEraseService, WindowsImageObjectEraseService>();
        services.AddSingleton<IVideoSuperResolutionService, WindowsVideoSuperResolutionService>();
        services.AddSingleton<IImageCanvasExporter, Win2DImageCanvasExporter>();
        services.AddSingleton<IImageCanvasPrinter, Win2DImageCanvasPrinter>();
        return services;
    }
}
