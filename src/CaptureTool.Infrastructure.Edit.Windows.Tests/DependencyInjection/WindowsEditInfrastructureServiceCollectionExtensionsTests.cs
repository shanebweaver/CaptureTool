using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.ChromaKey;
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
