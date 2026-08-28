using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Analysis.Processing;
using CaptureTool.Application.Abstractions.Analysis.Privacy;
using CaptureTool.Application.Abstractions.Analysis.Queries;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
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
using CaptureTool.Application.Capture;
using CaptureTool.Application.Edit.External;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Navigation;
using CaptureTool.Application.Storage;
using CaptureTool.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services
            .AddUseCaseServices()
            .AddActivationServices()
            .AddAppMenuUseCases()
            .AddAudioCaptureServices()
            .AddCaptureAnalysisServices()
            .AddCaptureAssetServices()
            .AddAudioEditUseCases()
            .AddCaptureOverlayUseCases()
            .AddDiagnosticsUseCases()
            .AddImageCaptureServices()
            .AddImageEditUseCases()
            .AddNavigationUseCases()
            .AddRecentCaptureServices()
            .AddSettingsUseCases()
            .AddStoreUseCases()
            .AddVideoCaptureServices()
            .AddVideoEditUseCases()
            .AddWindowingUseCases();

        return services;
    }

    private static IServiceCollection AddCaptureAnalysisServices(this IServiceCollection services)
    {
        services.AddSingleton<CaptureAnalyzerCatalog>();
        services.AddSingleton<ICaptureAnalyzerCatalog>(provider =>
            provider.GetRequiredService<CaptureAnalyzerCatalog>());
        services.AddSingleton<ICaptureAnalyzerSelectionService,
            AutomaticCaptureAnalyzerSelectionService>();
        services.AddSingleton<ICaptureAnalyzerResolutionPreference, CaptureAnalyzerResolutionPreference>();
        services.AddSingleton<ICaptureAnalyzerResolver, CaptureAnalyzerResolver>();
        services.AddSingleton<ICaptureAnalysisQueryService, CaptureAnalysisQueryService>();
        services.AddSingleton<CaptureAnalysisCapabilityPreparationService>();
        services.AddSingleton<IAnalysisCapabilityPreparationQueryService>(provider =>
            provider.GetRequiredService<CaptureAnalysisCapabilityPreparationService>());
        services.AddSingleton<IUserInitiatedAnalysisCapabilityPreparationService>(provider =>
            provider.GetRequiredService<CaptureAnalysisCapabilityPreparationService>());
        services.AddSingleton<CaptureAnalysisPolicyService>();
        services.AddSingleton<ICaptureAnalysisPolicyService>(provider =>
            provider.GetRequiredService<CaptureAnalysisPolicyService>());
        services.AddSingleton<ICaptureAnalysisScheduler, CaptureAnalysisScheduler>();
        services.AddSingleton<CaptureMemorySearchProjection>();
        services.AddSingleton<ICaptureMemorySearchService>(provider =>
            provider.GetRequiredService<CaptureMemorySearchProjection>());
        services.AddSingleton<ICaptureAnalysisProjectionRefresher>(provider =>
            provider.GetRequiredService<CaptureMemorySearchProjection>());
        services.AddSingleton<ICaptureAnalysisProjectionMaintenance>(provider =>
            provider.GetRequiredService<CaptureMemorySearchProjection>());
        services.AddSingleton<ICaptureAnalysisCleanupCoordinator, CaptureAnalysisCleanupCoordinator>();
        services.AddSingleton<ICaptureAnalysisPolicyCommandService, CaptureAnalysisPolicyCommandService>();
        services.AddSingleton<CaptureAnalysisLifecycleService>();
        services.AddSingleton<ICaptureAnalysisExclusionService>(provider =>
            provider.GetRequiredService<CaptureAnalysisLifecycleService>());
        services.AddSingleton<ICaptureAnalysisMaintenanceService>(provider =>
            provider.GetRequiredService<CaptureAnalysisLifecycleService>());
        services.AddSingleton<ICaptureAssetRemovalService>(provider =>
            provider.GetRequiredService<CaptureAnalysisLifecycleService>());
        services.AddSingleton<CaptureAssetChangeReader>();
        services.AddSingleton<ICaptureAssetChangeReader>(provider =>
            provider.GetRequiredService<CaptureAssetChangeReader>());
        services.AddSingleton<CaptureAnalysisIntakeService>();
        services.AddSingleton<ICaptureAnalysisReconciler>(provider =>
            provider.GetRequiredService<CaptureAnalysisIntakeService>());
        services.AddSingleton<ICaptureAnalysisBackfillService>(provider =>
            provider.GetRequiredService<CaptureAnalysisIntakeService>());
        services.AddSingleton<ICaptureAnalysisWorker, CaptureAnalysisWorker>();
        services.AddSingleton<CaptureAnalysisWorkerHost>();
        return services;
    }

    private static IServiceCollection AddUseCaseServices(this IServiceCollection services)
    {
        services.AddTransient<IUseCaseExecutor, UseCaseExecutor>();
        services.AddSingleton<IAiFeatureConsentService, AiFeatureConsentService>();
        services.AddTransient<IOpenExternalEditorUseCase, OpenExternalEditorUseCase>();
        services.AddSingleton<IActiveEditSessionService, ActiveEditSessionService>();
        services.AddSingleton<IEditSessionGuard, EditSessionGuard>();
        services.AddSingleton<INavigationCoordinator, NavigationCoordinator>();
        services.AddSingleton<CaptureFileAllocator>();
        services.AddSingleton<IScratchArtifactStore, ScratchArtifactStore>();

        return services;
    }
}
