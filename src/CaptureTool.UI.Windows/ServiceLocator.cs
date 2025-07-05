using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;

namespace CaptureTool.UI.Windows;

internal class ServiceLocator
{
    public static ILogService Logging => GetService<ILogService>();
    public static INavigationService Navigation => GetService<INavigationService>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
