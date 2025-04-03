using CaptureTool.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.UI;

internal static partial class ViewModelLocator
{
    public static T GetViewModel<T>() where T : ViewModelBase => App.Current.ServiceProvider.GetRequiredService<T>();
}