using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Presentation.Windows.WinUI.AudioCapture;
using CaptureTool.Presentation.Windows.WinUI.Capture;
using CaptureTool.Presentation.Windows.WinUI.CaptureMemory;
using CaptureTool.Presentation.Windows.WinUI.Edit;
using CaptureTool.Presentation.Windows.WinUI.EditSessions;
using CaptureTool.Presentation.Windows.WinUI.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.Windows.WinUI.DependencyInjection;

public static class WindowsPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddAppWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<AppNavigationHandler>();
        services.AddSingleton<INavigationHandler>(sp => sp.GetRequiredService<AppNavigationHandler>());
        services.AddSingleton<IWindowHandleProvider>(sp => sp.GetRequiredService<AppNavigationHandler>());
        services.AddSingleton<WinUIEditSessionConfirmationService>();
        services.AddSingleton<IEditSessionConfirmationService>(sp => sp.GetRequiredService<WinUIEditSessionConfirmationService>());
        services.AddSingleton<WinUIAudioCaptureNavigationConfirmationService>();
        services.AddSingleton<IAudioCaptureNavigationConfirmationService>(sp => sp.GetRequiredService<WinUIAudioCaptureNavigationConfirmationService>());
        services.AddSingleton<WinUICaptureDiscardConfirmationService>();
        services.AddSingleton<ICaptureDiscardConfirmationService>(sp => sp.GetRequiredService<WinUICaptureDiscardConfirmationService>());
        services.AddSingleton<CaptureMemoryConfirmationDialogService>();
        services.AddSingleton<ICaptureAnalysisSettingsConfirmationDialogService>(sp =>
            sp.GetRequiredService<CaptureMemoryConfirmationDialogService>());
        services.AddSingleton<AiFeatureConsentDialogService>();
        services.AddSingleton<IAiFeatureConsentDialogService>(sp => sp.GetRequiredService<AiFeatureConsentDialogService>());
        services.AddSingleton<ImageSuperResolutionPreparationConsentService>();
        services.AddSingleton<IImageSuperResolutionPreparationConsentService>(sp => sp.GetRequiredService<ImageSuperResolutionPreparationConsentService>());
        services.AddSingleton<TelemetryConsentDialogService>();
        return services;
    }
}
