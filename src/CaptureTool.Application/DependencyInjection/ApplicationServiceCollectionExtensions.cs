using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Ai;
using CaptureTool.Application.Analysis;
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

    private static IServiceCollection AddUseCaseServices(this IServiceCollection services)
    {
        services.AddTransient<IUseCaseExecutor, UseCaseExecutor>();
        services.AddSingleton<IAiFeatureConsentService, AiFeatureConsentService>();
        services.AddSingleton(CaptureAnalysisConfiguration.CreateDefault());
        services.AddSingleton<IMetadataProcessor, StructuredFactsProcessor>();
        services.AddSingleton<CaptureMemoryAuthorization>();
        services.AddSingleton<IAnalysisAuthorization>(provider => provider.GetRequiredService<CaptureMemoryAuthorization>());
        services.AddSingleton<ICaptureAnalysisWorker, CaptureAnalysisWorker>();
        services.AddSingleton<CaptureTool.Application.Capture.CaptureNamingService>();
        services.AddSingleton<CaptureTool.Application.Abstractions.Capture.Assets.ICaptureNamingService>(provider => provider.GetRequiredService<CaptureTool.Application.Capture.CaptureNamingService>());
        services.AddSingleton<ICaptureMemoryService, CaptureMemoryService>();
        services.AddTransient<CaptureTool.Application.Abstractions.Library.CaptureDetails.ICaptureDetailsReader,
            CaptureTool.Application.Library.CaptureDetails.CaptureDetailsReader>();
        services.AddSingleton<CaptureAnalysisIntake>();
        services.AddTransient<IOpenExternalEditorUseCase, OpenExternalEditorUseCase>();
        services.AddSingleton<IActiveEditSessionService, ActiveEditSessionService>();
        services.AddSingleton<IEditSessionGuard, EditSessionGuard>();
        services.AddSingleton<INavigationCoordinator, NavigationCoordinator>();
        services.AddSingleton<CaptureFileAllocator>();
        services.AddSingleton<IScratchArtifactStore, ScratchArtifactStore>();

        return services;
    }
}
