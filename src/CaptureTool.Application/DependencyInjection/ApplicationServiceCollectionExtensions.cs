using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Ai;
using CaptureTool.Application.Capture;
using CaptureTool.Application.Edit.External;
using CaptureTool.Application.EditSessions;
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
        services.AddTransient<IOpenExternalEditorUseCase, OpenExternalEditorUseCase>();
        services.AddSingleton<IActiveEditSessionService, ActiveEditSessionService>();
        services.AddSingleton<IEditSessionGuard, EditSessionGuard>();
        services.AddSingleton<CaptureFileAllocator>();

        return services;
    }
}
