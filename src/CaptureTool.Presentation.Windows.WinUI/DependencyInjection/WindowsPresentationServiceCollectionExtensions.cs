using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Presentation.Windows.WinUI.AudioCapture;
using CaptureTool.Presentation.Windows.WinUI.EditSessions;
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
        return services;
    }
}
