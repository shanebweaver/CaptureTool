using CaptureTool.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI.Windows;

internal static partial class ViewModelLocator
{
    public static T GetViewModel<T>() where T : ViewModelBase => App.Current.ServiceProvider.GetRequiredService<T>();
}