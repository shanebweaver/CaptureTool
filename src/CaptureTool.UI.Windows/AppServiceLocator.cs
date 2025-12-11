using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.FeatureManagement;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Shutdown;

namespace CaptureTool.UI.Windows;

internal class AppServiceLocator
{
    public static IShutdownHandler ShutdownHandler => GetService<IShutdownHandler>();
    public static ILogService Logging => GetService<ILogService>();
    public static IAppNavigation Navigation => GetService<IAppNavigation>();
    public static IFeatureManager FeatureManager => GetService<IFeatureManager>();
    public static IImageCaptureHandler ImageCapture => GetService<IImageCaptureHandler>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
