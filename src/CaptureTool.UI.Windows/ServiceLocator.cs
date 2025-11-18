using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Logging;

namespace CaptureTool.UI.Windows;

internal class ServiceLocator
{
    public static IAppController AppController => GetService<IAppController>();
    public static ILogService Logging => GetService<ILogService>();
    public static IAppNavigation Navigation => GetService<IAppNavigation>();
    public static IFeatureManager FeatureManager => GetService<IFeatureManager>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
