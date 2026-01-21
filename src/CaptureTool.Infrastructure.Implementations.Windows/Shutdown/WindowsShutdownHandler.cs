using CaptureTool.Infrastructure.Interfaces.Cancellation;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Shutdown;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.Infrastructure.Implementations.Windows.Shutdown;

public sealed partial class WindowsShutdownHandler : IShutdownHandler
{
    private readonly ICancellationService _cancellationService;
    private readonly ILogService _logService;

    public bool IsShuttingDown { get; private set; }

    public WindowsShutdownHandler(
        ILogService logService,
        ICancellationService cancellationService) 
    {
        _logService = logService;
        _cancellationService = cancellationService;
    }

    public bool TryRestart()
    {
        if (IsShuttingDown)
        {
            // Can't restart, shutdown in progress.
            return false;
        }

        Teardown();
        global::Windows.ApplicationModel.Core.AppRestartFailureReason restartError = AppInstance.Restart(string.Empty);

        switch (restartError)
        {
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.NotInForeground:
                _logService.LogWarning("The app is not in the foreground.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.RestartPending:
                _logService.LogWarning("Another restart is currently pending.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.InvalidUser:
                _logService.LogWarning("Current user is not signed in or not a valid user.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.Other:
                _logService.LogWarning("Failure restarting.");
                break;
        }

        return false;
    }

    public void Shutdown()
    {
        if (IsShuttingDown)
        {
            return;
        }

        try
        {
            Teardown();
        }
        catch (Exception e)
        {
            _logService.LogException(e, "Error during shutdown. Forcing exit.");
        }

        Environment.Exit(0);
    }

    private void Teardown()
    {
        IsShuttingDown = true;
        _cancellationService.CancelAll();
    }
}
