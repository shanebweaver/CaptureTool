using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Infrastructure.Windows.Shutdown;
using CaptureTool.Presentation.Windows.WinUI.Analysis;
using CaptureTool.Presentation.Windows.WinUI.AudioCapture;
using CaptureTool.Presentation.Windows.WinUI.Capture;
using CaptureTool.Presentation.Windows.WinUI.EditSessions;
using CaptureTool.Presentation.Windows.WinUI.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.Windows.WinUI.DependencyInjection;

public static class WindowsPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddAppWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<IShutdownHandler>(sp => new WindowsShutdownHandler(
            sp.GetRequiredService<ILogService>(),
            sp.GetRequiredService<ICancellationService>(),
            sp.GetService<ITelemetryService>(),
            sp.GetService<ICaptureMemoryService>(),
            App.Current.Exit,
            App.Current.PrepareForShutdownAsync));
        services.AddSingleton<AppNavigationHandler>();
        services.AddSingleton<INavigationHandler>(sp => sp.GetRequiredService<AppNavigationHandler>());
        services.AddSingleton<IWindowHandleProvider>(sp => sp.GetRequiredService<AppNavigationHandler>());
        services.AddSingleton<WinUIEditSessionConfirmationService>();
        services.AddSingleton<IEditSessionConfirmationService>(sp => sp.GetRequiredService<WinUIEditSessionConfirmationService>());
        services.AddSingleton<WinUIAudioCaptureNavigationConfirmationService>();
        services.AddSingleton<IAudioCaptureNavigationConfirmationService>(sp => sp.GetRequiredService<WinUIAudioCaptureNavigationConfirmationService>());
        services.AddSingleton<WinUICaptureDiscardConfirmationService>();
        services.AddSingleton<ICaptureDiscardConfirmationService>(sp => sp.GetRequiredService<WinUICaptureDiscardConfirmationService>());
        services.AddSingleton<WinUIMicrophoneAccessPrompt>();
        services.AddSingleton<IMicrophoneAccessPrompt>(sp => sp.GetRequiredService<WinUIMicrophoneAccessPrompt>());
        services.AddSingleton<TelemetryConsentDialogService>();
        services.AddSingleton<CaptureTool.Presentation.Windows.WinUI.Utils.MainWindowDialogCoordinator>();
        services.AddSingleton<CaptureMemoryDialogService>();
        services.AddSingleton<ICaptureMemoryPrompts>(sp => sp.GetRequiredService<CaptureMemoryDialogService>());
        return services;
    }
}
