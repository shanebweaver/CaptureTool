using CaptureTool.Services.Localization;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Capture.Windows.SnippingTool;
using CaptureTool.Core.AppController;

namespace CaptureTool.UI.Windows;

internal class ServiceLocator
{
    public static IAppController AppController => GetService<IAppController>();
    public static ILogService Logging => GetService<ILogService>();
    public static INavigationService Navigation => GetService<INavigationService>();
    public static ILocalizationService Localization => GetService<ILocalizationService>();
    public static ISnippingToolService SnippingToolService => GetService<ISnippingToolService>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
