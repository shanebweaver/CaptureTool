using CaptureTool.Services.Logging;
using CaptureTool.Services.SnippingTool;

namespace CaptureTool.UI;

internal class ServiceLocator
{
    public static ILogService Logging => GetService<ILogService>();
    public static ISnippingToolService SnippingToolService => GetService<ISnippingToolService>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
