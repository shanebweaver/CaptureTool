using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Checkpoints;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Analysis.Processing;
using CaptureTool.Application.Abstractions.Analysis.Privacy;
using CaptureTool.Application.Abstractions.Analysis.Queries;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureImage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Activation;
using CaptureTool.Application.Ai;
using CaptureTool.Application.Analysis.Policy;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Application.Analysis.Intake;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Application.Analysis.Memory;
using CaptureTool.Application.Analysis.Orchestration;
using CaptureTool.Application.Analysis.Preparation;
using CaptureTool.Application.Analysis.Processing;
using CaptureTool.Application.Analysis.Queries;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Application.Capture.Image;
using CaptureTool.Application.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Capture.Image.CaptureImage;
using CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.Capture.Video.StartVideoCapture;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Library.RecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Library.CaptureMemory;
using CaptureTool.Application.Navigation;
using CaptureTool.Application.Settings.OpenSettingsPage;
using CaptureTool.Application.Storage;
using CaptureTool.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CaptureTool.Application.Tests.DependencyInjection;

[TestClass]
public sealed class ApplicationServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddApplicationServices_ShouldRegisterCoreApplicationServices()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddApplicationServices();

        Assert.AreSame(services, result);
        AssertHasRegistration<IUseCaseExecutor, UseCaseExecutor>(services, ServiceLifetime.Transient);
        AssertHasRegistration<IAiFeatureConsentService, AiFeatureConsentService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureAnalysisPolicyService, CaptureAnalysisPolicyService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalysisPolicyService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalysisPolicyCommandService, CaptureAnalysisPolicyCommandService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureAnalyzerCatalog, CaptureAnalyzerCatalog>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalyzerCatalog>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalyzerResolutionPreference, CaptureAnalyzerResolutionPreference>(
            services,
            ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalyzerSelectionService,
            AutomaticCaptureAnalyzerSelectionService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalyzerResolver, CaptureAnalyzerResolver>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalysisQueryService, CaptureAnalysisQueryService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureAnalysisCapabilityPreparationService, CaptureAnalysisCapabilityPreparationService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IAnalysisCapabilityPreparationQueryService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IUserInitiatedAnalysisCapabilityPreparationService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalysisScheduler, CaptureAnalysisScheduler>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureAssetChangeReader, CaptureAssetChangeReader>(
            services,
            ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAssetChangeReader>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureAnalysisIntakeService, CaptureAnalysisIntakeService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalysisReconciler>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalysisBackfillService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalysisWorker, CaptureAnalysisWorker>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureMemorySearchProjection, CaptureMemorySearchProjection>(
            services,
            ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureMemorySearchService>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalysisProjectionRefresher>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalysisProjectionMaintenance>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAnalysisCleanupCoordinator, CaptureAnalysisCleanupCoordinator>(
            services,
            ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureAnalysisLifecycleService, CaptureAnalysisLifecycleService>(
            services,
            ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalysisExclusionService>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAnalysisMaintenanceService>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<ICaptureAssetRemovalService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<CaptureAnalysisWorkerHost, CaptureAnalysisWorkerHost>(
            services,
            ServiceLifetime.Singleton);
        AssertHasRegistration<IApplicationStartupInitializer, ApplicationStartupInitializer>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAssetBootstrapper, CaptureAssetBootstrapper>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAssetLifecycleService, CaptureAssetLifecycleService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ILaunchNavigationTargetProvider, DefaultLaunchNavigationTargetProvider>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IActiveEditSessionService, ActiveEditSessionService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IEditSessionGuard, EditSessionGuard>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<INavigationCoordinator, NavigationCoordinator>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IScratchArtifactStore, ScratchArtifactStore>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IRecentCapturesChangeNotifier, RecentCapturesChangeNotifier>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IClearRecentCapturesUseCase, ClearRecentCapturesUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<IRemoveRecentCaptureUseCase, RemoveRecentCaptureUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<ICaptureMemoryResultResolver, CaptureMemoryResultResolver>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IOpenCaptureMemoryResultUseCase, OpenCaptureMemoryResultUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<AudioCaptureWorkflow, AudioCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IAudioCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IAudioCaptureState>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICancelAudioCaptureUseCase, CancelAudioCaptureUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<VideoCaptureWorkflow, VideoCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IVideoCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IVideoCaptureState>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IStartVideoCaptureUseCase, StartVideoCaptureUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<ImageCaptureWorkflow, ImageCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IImageCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IImageCaptureState>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAllScreensImageUseCase, CaptureAllScreensImageUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<ICaptureImageUseCase, CaptureImageUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<IOpenSelectionOverlayUseCase, OpenSelectionOverlayUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<IOpenSettingsPageUseCase, OpenSettingsPageUseCase>(services, ServiceLifetime.Transient);
    }

    [TestMethod]
    public void AddApplicationServices_ShouldResolvePolicyCommandsWithoutCircularDependency()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddSingleton(Mock.Of<ICaptureAssetCatalog>());
        services.AddSingleton(Mock.Of<ICaptureAnalysisControlStore>());
        services.AddSingleton(Mock.Of<ICaptureAnalysisFeatureAvailability>());
        services.AddSingleton(Mock.Of<ISettingsService>());
        services.AddSingleton(Mock.Of<ICaptureAnalysisJobStore>());
        services.AddSingleton(Mock.Of<ICaptureAnalysisCheckpointStore>());
        services.AddSingleton(Mock.Of<ICaptureAnalysisStore>());
        services.AddSingleton(Mock.Of<ICaptureAnalysisMutationCoordinator>());
        services.AddSingleton(Mock.Of<ICaptureAnalysisProjectionMaintenance>());
        services.AddSingleton(Mock.Of<IRecentCaptureCatalog>());
        services.AddSingleton(Mock.Of<IFileSystem>());
        using ServiceProvider provider = services.BuildServiceProvider();

        ICaptureAnalysisPolicyCommandService commands =
            provider.GetRequiredService<ICaptureAnalysisPolicyCommandService>();

        Assert.IsInstanceOfType<CaptureAnalysisPolicyCommandService>(commands);
    }

    private static void AssertHasRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation) &&
            descriptor.Lifetime == lifetime));
    }

    private static void AssertHasFactoryRegistration<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationFactory is not null &&
            descriptor.Lifetime == lifetime));
    }
}
