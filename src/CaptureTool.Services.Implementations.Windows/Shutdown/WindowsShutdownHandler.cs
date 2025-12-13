using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Shutdown;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;

namespace CaptureTool.Services.Implementations.Windows.Shutdown;

public sealed partial class WindowsShutdownHandler : IShutdownHandler
{
    private readonly ICancellationService _cancellationService;
    private readonly ILogService _logService;
    private readonly object _lockObject = new();

    public bool IsShuttingDown { get; private set; }

    public event EventHandler? ShutdownRequested;

    public WindowsShutdownHandler(
        ILogService logService,
        ICancellationService cancellationService) 
    {
        _logService = logService;
        _cancellationService = cancellationService;
    }

    public bool TryRestart()
    {
        lock (_lockObject)
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
    }

    public void Shutdown()
    {
        lock (_lockObject)
        {
            if (IsShuttingDown)
            {
                return;
            }
            
            IsShuttingDown = true;

            try
            {
                Teardown();
                Application.Current.Exit();
            }
            catch (Exception e)
            {
                Debug.Fail($"Error during shutdown: {e.Message}");
                Environment.Exit(0);
            }
        }
    }

    public void NotifyMainWindowClosed()
    {
        lock (_lockObject)
        {
            if (IsShuttingDown)
            {
                return;
            }
            
            IsShuttingDown = true;

            // When the main window is closed by the user (e.g., clicking X button),
            // we just need to clean up but NOT call Application.Exit() or Environment.Exit().
            // The WinUI framework will handle the application shutdown naturally.
            // Calling Exit() here causes race conditions and crashes.
            try
            {
                Teardown();
            }
            catch (Exception e)
            {
                _logService.LogException(e, "Error during main window closure cleanup.");
            }
        }
    }

    private void Teardown()
    {
        _cancellationService.CancelAll();
        ShutdownRequested?.Invoke(this, EventArgs.Empty);
    }
}
