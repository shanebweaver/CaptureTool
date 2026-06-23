using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shutdown;

namespace CaptureTool.Presentation.Windows.WinUI;

internal class AppServiceLocator
{
    public static IShutdownHandler ShutdownHandler => GetService<IShutdownHandler>();
    public static ILogService Logging => GetService<ILogService>();
    public static INavigationService Navigation => GetService<INavigationService>();
    public static IEditSessionGuard EditSessionGuard => GetService<IEditSessionGuard>();
    public static IAudioCaptureNavigationGuard AudioCaptureNavigationGuard => GetService<IAudioCaptureNavigationGuard>();
    public static IImageCaptureHandler ImageCapture => GetService<IImageCaptureHandler>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
