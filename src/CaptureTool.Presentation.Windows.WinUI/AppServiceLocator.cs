using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Shutdown;

namespace CaptureTool.Presentation.Windows.WinUI;

internal class AppServiceLocator
{
    public static IShutdownHandler ShutdownHandler => GetService<IShutdownHandler>();
    public static ILogService Logging => GetService<ILogService>();
    public static IAppNavigation Navigation => GetService<IAppNavigation>();
    public static IFeatureManager FeatureManager => GetService<IFeatureManager>();
    public static IImageCaptureHandler ImageCapture => GetService<IImageCaptureHandler>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
