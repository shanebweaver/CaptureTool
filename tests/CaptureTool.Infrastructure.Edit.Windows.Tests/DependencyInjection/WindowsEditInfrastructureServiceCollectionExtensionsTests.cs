using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Infrastructure.Edit.Windows.ChromaKey;
using CaptureTool.Infrastructure.Edit.Windows.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests.DependencyInjection;

[TestClass]
public sealed class WindowsEditInfrastructureServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddWindowsEditDomains_RegistersEditServices()
    {
        var services = new ServiceCollection();

        services.AddWindowsEditDomains();

        services.ShouldContainSingleton<IChromaKeyService, Win2DChromaKeyService>();
        services.ShouldContainSingleton<IImageSuperResolutionService, WindowsImageSuperResolutionService>();
        services.ShouldContainSingleton<ITextExtractionService, WindowsTextExtractionService>();
        services.ShouldContainSingleton<IImageDescriptionService, WindowsImageDescriptionService>();
        services.ShouldContainSingleton<IImageForegroundExtractionService, WindowsImageForegroundExtractionService>();
        services.ShouldContainSingleton<IImageObjectEraseService, WindowsImageObjectEraseService>();
        services.ShouldContainSingleton<IVideoSuperResolutionService, WindowsVideoSuperResolutionService>();
        services.ShouldContainSingleton<IImageCanvasExporter, Win2DImageCanvasExporter>();
        services.ShouldContainSingleton<IImageCanvasPrinter, Win2DImageCanvasPrinter>();
    }
}

file static class ServiceCollectionAssertions
{
    public static void ShouldContainSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(TService)
            && descriptor.ImplementationType == typeof(TImplementation)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
