using CaptureTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

internal static partial class ViewModelLocator
{
    public static MainWindowViewModel MainWindow => GetService<MainWindowViewModel>();
    public static HomePageViewModel HomePage => GetService<HomePageViewModel>();
    public static StartupPageViewModel StartupPage => GetService<StartupPageViewModel>();
    public static SettingsPageViewModel SettingsPage => GetService<SettingsPageViewModel>();

    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetRequiredService<T>();
}