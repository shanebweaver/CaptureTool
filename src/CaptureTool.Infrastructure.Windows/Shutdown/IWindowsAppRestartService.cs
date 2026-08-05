using Windows.ApplicationModel.Core;

namespace CaptureTool.Infrastructure.Windows.Shutdown;

internal interface IWindowsAppRestartService
{
    AppRestartFailureReason Restart(string arguments);
}

internal sealed class WindowsAppRestartService : IWindowsAppRestartService
{
    public AppRestartFailureReason Restart(string arguments)
        => Microsoft.Windows.AppLifecycle.AppInstance.Restart(arguments);
}
