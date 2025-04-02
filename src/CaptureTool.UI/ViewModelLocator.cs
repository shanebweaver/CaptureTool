using CaptureTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

internal static partial class ViewModelLocator
{
    public static MainWindowViewModel MainWindow => GetService<MainWindowViewModel>();
    public static HomePageViewModel HomePage => GetService<HomePageViewModel>();
    public static SettingsPageViewModel SettingsPage => GetService<SettingsPageViewModel>();
    public static DesktopCaptureResultsViewModel DesktopCaptureResultsPage => GetService<DesktopCaptureResultsViewModel>();

    public static AppMenuViewModel AppMenuView => GetService<AppMenuViewModel>();
    public static AppTitleBarViewModel AppTitleBarView => GetService<AppTitleBarViewModel>();

    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetRequiredService<T>();
}