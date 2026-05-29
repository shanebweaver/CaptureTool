using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Presentation.Windows.WinUI;

internal class AppServiceLocator
{
    public static IShutdownHandler ShutdownHandler => GetService<IShutdownHandler>();
    public static ILogService Logging => GetService<ILogService>();
    public static INavigationService Navigation => GetService<INavigationService>();
    public static IImageCaptureHandler ImageCapture => GetService<IImageCaptureHandler>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
