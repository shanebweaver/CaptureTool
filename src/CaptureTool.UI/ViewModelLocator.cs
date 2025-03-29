using CaptureTool.ViewModels;

namespace CaptureTool.UI;

internal static partial class ViewModelLocator
{
    public static MainWindowViewModel MainWindow => GetService<MainWindowViewModel>();
    public static HomePageViewModel HomePage => GetService<HomePageViewModel>();
    private static T GetService<T>() where T : notnull => App.Current.Ioc.GetService<T>();
}