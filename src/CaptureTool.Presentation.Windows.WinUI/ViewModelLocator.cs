using CaptureTool.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Presentation.Windows.WinUI;

internal static partial class ViewModelLocator
{
    public static T GetViewModel<T>() where T : IViewModel => App.Current.ServiceProvider.GetRequiredService<T>();
}