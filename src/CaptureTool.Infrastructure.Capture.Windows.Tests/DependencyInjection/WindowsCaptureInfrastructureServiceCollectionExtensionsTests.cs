using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.DependencyInjection;

[TestClass]
public sealed class WindowsCaptureInfrastructureServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddWindowsCaptureDomains_RegistersCaptureServices()
    {
        var services = new ServiceCollection();

        services.AddWindowsCaptureDomains();

        services.ShouldContainSingleton<IScreenCapture, WindowsScreenCapture>();
        services.ShouldContainSingleton<IScreenRecorder, WindowsScreenRecorder>();
        services.ShouldContainSingleton<IAudioRecorder, WindowsAudioRecorder>();
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
