using CaptureTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

internal static partial class ViewModelLocator
{
    public static MainWindowViewModel MainWindow => GetService<MainWindowViewModel>();

    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetRequiredService<T>();
}