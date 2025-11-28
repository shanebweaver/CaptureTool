using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Interfaces.Logging;

namespace CaptureTool.UI.Windows;

internal class ServiceLocator
{
    public static IAppController AppController => GetService<IAppController>();
    public static ILogService Logging => GetService<ILogService>();
    public static IAppNavigation Navigation => GetService<IAppNavigation>();
    public static IFeatureManager FeatureManager => GetService<IFeatureManager>();
    public static IImageCaptureHandler ImageCapture => GetService<IImageCaptureHandler>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
