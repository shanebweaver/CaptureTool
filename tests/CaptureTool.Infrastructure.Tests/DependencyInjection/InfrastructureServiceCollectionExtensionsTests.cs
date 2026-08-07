using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Globalization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Infrastructure.Cancellation;
using CaptureTool.Infrastructure.CaptureAssets;
using CaptureTool.Infrastructure.DependencyInjection;
using CaptureTool.Infrastructure.Features;
using CaptureTool.Infrastructure.Files;
using CaptureTool.Infrastructure.Globalization;
using CaptureTool.Infrastructure.Logging;
using CaptureTool.Infrastructure.Metrics;
using CaptureTool.Infrastructure.Navigation;
using CaptureTool.Infrastructure.RecentCaptures;
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
        AssertHasRegistration<ICaptureAssetCatalog, LocalCaptureAssetCatalog>(services);
        AssertHasRegistration<ICaptureAssetChangeSignal, NullCaptureAssetChangeSignal>(services);
        AssertHasRegistration<ICaptureAnalysisFeatureAvailability, CaptureAnalysisFeatureAvailability>(services);
        AssertHasRegistration<IAiConsentSettingsFeatureAvailability, AiConsentSettingsFeatureAvailability>(services);
        AssertHasRegistration<IStoreFeatureAvailability, StoreFeatureAvailability>(services);
        AssertHasRegistration<IChromaKeyFeatureAvailability, ChromaKeyFeatureAvailability>(services);
        AssertHasRegistration<IImageSuperResolutionFeatureAvailability, ImageSuperResolutionFeatureAvailability>(services);
        AssertHasRegistration<ITextExtractionFeatureAvailability, TextExtractionFeatureAvailability>(services);
        AssertHasRegistration<IImageDescriptionFeatureAvailability, ImageDescriptionFeatureAvailability>(services);
        AssertHasRegistration<IImageForegroundExtractionFeatureAvailability, ImageForegroundExtractionFeatureAvailability>(services);
        AssertHasRegistration<IImageObjectEraseFeatureAvailability, ImageObjectEraseFeatureAvailability>(services);
        AssertHasRegistration<IImageObjectExtractionFeatureAvailability, ImageObjectExtractionFeatureAvailability>(services);
        AssertHasRegistration<IVideoSuperResolutionFeatureAvailability, VideoSuperResolutionFeatureAvailability>(services);
        AssertHasRegistration<IFileSystem, LocalFileSystem>(services);
        AssertHasRegistration<IGlobalizationService, GlobalizationService>(services);
        AssertHasRegistration<INavigationService, NavigationService>(services);
        AssertHasRegistration<ISettingsService, LocalSettingsService>(services);
        AssertHasRegistration<IAppMetricsService, LocalAppMetricsService>(services);
        AssertHasRegistration<IRecentCaptureCatalog, LocalRecentCaptureCatalog>(services);
        AssertHasRegistration<IBackgroundTaskRunner, BackgroundTaskRunner>(services);
        AssertHasRegistration<ITelemetryConsentService, TelemetryConsentService>(services);
        AssertHasRegistration<ITelemetryEventSink, NullTelemetryService>(services);
        AssertHasRegistration<ITelemetryService, ConsentAwareTelemetryService>(services);
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
