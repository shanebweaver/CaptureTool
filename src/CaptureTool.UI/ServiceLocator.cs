using CaptureTool.Services.Logging;
using CaptureTool.Services.Settings;

namespace CaptureTool.UI;

internal class ServiceLocator
{
    public static ILogService Logging => GetService<ILogService>();
    private static T GetService<T>() where T : notnull => App.Current.ServiceProvider.GetService<T>();
}
