using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Ai;
using CaptureTool.Application.Analysis.Policy;
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
        services.AddSingleton<CaptureAnalysisPolicyService>();
        services.AddSingleton<ICaptureAnalysisPolicyService>(provider =>
            provider.GetRequiredService<CaptureAnalysisPolicyService>());
        services.AddSingleton<ICaptureAnalysisPolicyCommandService>(provider =>
            provider.GetRequiredService<CaptureAnalysisPolicyService>());
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
