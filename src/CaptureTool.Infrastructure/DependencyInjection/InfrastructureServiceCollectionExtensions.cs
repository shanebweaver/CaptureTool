using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Cancellation;
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
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.Analysis.Jobs;
using CaptureTool.Infrastructure.Analysis.Sources;
using CaptureTool.Infrastructure.CaptureAssets;
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

namespace CaptureTool.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGenericServices(this IServiceCollection services)
    {
        services.AddSingleton<ICancellationService, CancellationService>();
        services.AddSingleton<IAtomicFileWriter, AtomicFileWriter>();
        services.AddSingleton<ICaptureAnalysisControlStore, LocalCaptureAnalysisControlStore>();
        services.AddSingleton<LocalCaptureAnalysisStore>();
        services.AddSingleton<ICaptureAnalysisStore>(provider =>
            provider.GetRequiredService<LocalCaptureAnalysisStore>());
        services.AddSingleton<ICaptureAnalysisJobStore, LocalCaptureAnalysisJobStore>();
        services.AddSingleton<ICaptureAnalysisSourceVerifier, LocalCaptureAnalysisSourceVerifier>();
        services.AddSingleton<ICaptureAnalysisMutationCoordinator, CaptureAnalysisMutationCoordinator>();
        services.AddSingleton<ICaptureAssetCatalog, LocalCaptureAssetCatalog>();
        services.AddSingleton<CaptureAnalysisWakeChannel>();
        services.AddSingleton<ICaptureAssetChangeSignal>(provider =>
            provider.GetRequiredService<CaptureAnalysisWakeChannel>());
        services.AddSingleton<ICaptureAnalysisWakeSignal>(provider =>
            provider.GetRequiredService<CaptureAnalysisWakeChannel>());
        services.AddSingleton<ICaptureAnalysisWakeWaiter>(provider =>
            provider.GetRequiredService<CaptureAnalysisWakeChannel>());
        services.AddSingleton<ICaptureAnalysisFeatureAvailability, CaptureAnalysisFeatureAvailability>();
        services.AddSingleton<ICaptureMemoryFeatureAvailability, CaptureMemoryFeatureAvailability>();
        services.AddSingleton<IAiConsentSettingsFeatureAvailability, AiConsentSettingsFeatureAvailability>();
        services.AddSingleton<IStoreFeatureAvailability, StoreFeatureAvailability>();
        services.AddSingleton<IChromaKeyFeatureAvailability, ChromaKeyFeatureAvailability>();
        services.AddSingleton<IImageSuperResolutionFeatureAvailability, ImageSuperResolutionFeatureAvailability>();
        services.AddSingleton<ITextExtractionFeatureAvailability, TextExtractionFeatureAvailability>();
        services.AddSingleton<IImageDescriptionFeatureAvailability, ImageDescriptionFeatureAvailability>();
        services.AddSingleton<IImageForegroundExtractionFeatureAvailability, ImageForegroundExtractionFeatureAvailability>();
        services.AddSingleton<IImageObjectEraseFeatureAvailability, ImageObjectEraseFeatureAvailability>();
        services.AddSingleton<IImageObjectExtractionFeatureAvailability, ImageObjectExtractionFeatureAvailability>();
        services.AddSingleton<IVideoSuperResolutionFeatureAvailability, VideoSuperResolutionFeatureAvailability>();
        services.AddSingleton<IFileSystem, LocalFileSystem>();
        services.AddSingleton<IGlobalizationService, GlobalizationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, LocalSettingsService>();
        services.AddSingleton<IAppMetricsService, LocalAppMetricsService>();
        services.AddSingleton<IRecentCaptureCatalog, LocalRecentCaptureCatalog>();
        services.AddSingleton<IBackgroundTaskRunner, BackgroundTaskRunner>();
        services.AddSingleton<ITelemetryConsentService, TelemetryConsentService>();
        services.AddSingleton<ITelemetryEventSink, NullTelemetryService>();
        services.AddSingleton<ITelemetryService, ConsentAwareTelemetryService>();
        services.AddSingleton<IClock, SystemClock>();
#if DEBUG
        services.AddSingleton<ILogService, DebugLogService>();
#else
        services.AddSingleton<ILogService, ShortTermMemoryLogService>();
#endif
        return services;
    }
}
