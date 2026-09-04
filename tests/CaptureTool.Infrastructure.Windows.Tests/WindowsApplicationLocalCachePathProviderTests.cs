using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Infrastructure.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Windows.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Windows.Tests;

[TestClass]
public sealed class WindowsApplicationLocalCachePathProviderTests
{
    [TestMethod]
    public void AddWindowsServices_ShouldRegisterTheApplicationLocalCachePathBoundary()
    {
        var services = new ServiceCollection();

        services.AddWindowsServices(dispatcherQueue: null!);

        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IApplicationLocalCachePathProvider) &&
            descriptor.ImplementationType == typeof(WindowsApplicationLocalCachePathProvider) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
    }
}
