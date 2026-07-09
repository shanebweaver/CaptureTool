using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Globalization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Infrastructure.Cancellation;
using CaptureTool.Infrastructure.DependencyInjection;
using CaptureTool.Infrastructure.Features;
using CaptureTool.Infrastructure.Files;
using CaptureTool.Infrastructure.Globalization;
using CaptureTool.Infrastructure.Logging;
using CaptureTool.Infrastructure.Navigation;
using CaptureTool.Infrastructure.Settings;
using CaptureTool.Infrastructure.TaskEnvironment;
using CaptureTool.Infrastructure.Telemetry;
using CaptureTool.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Tests.DependencyInjection;

[TestClass]
public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddGenericServices_ShouldRegisterInfrastructureServices()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddGenericServices();

        Assert.AreSame(services, result);
        AssertHasRegistration<ICancellationService, CancellationService>(services);
        AssertHasRegistration<IStoreFeatureAvailability, StoreFeatureAvailability>(services);
        AssertHasRegistration<IChromaKeyFeatureAvailability, ChromaKeyFeatureAvailability>(services);
        AssertHasRegistration<IImageSuperResolutionFeatureAvailability, ImageSuperResolutionFeatureAvailability>(services);
        AssertHasRegistration<IFileSystem, LocalFileSystem>(services);
        AssertHasRegistration<IGlobalizationService, GlobalizationService>(services);
        AssertHasRegistration<INavigationService, NavigationService>(services);
        AssertHasRegistration<ISettingsService, LocalSettingsService>(services);
        AssertHasRegistration<IBackgroundTaskRunner, BackgroundTaskRunner>(services);
        AssertHasRegistration<ITelemetryService, TelemetryService>(services);
        AssertHasRegistration<IClock, SystemClock>(services);
#if DEBUG
        AssertHasRegistration<ILogService, DebugLogService>(services);
#else
        AssertHasRegistration<ILogService, ShortTermMemoryLogService>(services);
#endif
    }

    private static void AssertHasRegistration<TService, TImplementation>(IServiceCollection services)
    {
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
    }
}
