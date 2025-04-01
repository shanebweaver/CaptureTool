using CaptureTool.Services.AppController;
using CaptureTool.Services.Logging;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace CaptureTool.UI;

internal class CaptureToolAppController : IAppController
{
    private readonly ILogService _logService;

    public CaptureToolAppController(ILogService logService) 
    {
        _logService = logService;
    }

    public bool TryRestart()
    {
        AppRestartFailureReason restartError = AppInstance.Restart(string.Empty);

        switch (restartError)
        {
            case AppRestartFailureReason.NotInForeground:
                _logService.LogWarning("The app is not in the foreground.");
                break;
            case AppRestartFailureReason.RestartPending:
                _logService.LogWarning("Another restart is currently pending.");
                break;
            case AppRestartFailureReason.InvalidUser:
                _logService.LogWarning("Current user is not signed in or not a valid user.");
                break;
            case AppRestartFailureReason.Other:
                _logService.LogWarning("Failure restarting.");
                break;
        }

        return false;
    }

    public void Shutdown()
    {
        App.Current.Shutdown();
    }
}
