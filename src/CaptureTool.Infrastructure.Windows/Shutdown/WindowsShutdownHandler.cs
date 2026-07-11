using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Telemetry;
using Microsoft.Windows.AppLifecycle;

namespace CaptureTool.Infrastructure.Windows.Shutdown;

public sealed partial class WindowsShutdownHandler : IShutdownHandler
{
    private readonly ICancellationService _cancellationService;
    private readonly ILogService _logService;
    private readonly ITelemetryService _telemetryService;

    public bool IsShuttingDown { get; private set; }

    public WindowsShutdownHandler(
        ILogService logService,
        ICancellationService cancellationService,
        ITelemetryService telemetryService)
    {
        _logService = logService;
        _cancellationService = cancellationService;
        _telemetryService = telemetryService;
    }

    public bool TryRestart()
    {
        if (IsShuttingDown)
        {
            // Can't restart, shutdown in progress.
            return false;
        }

        TrackShutdownRequested("restart");
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
            TrackShutdownRequested("exit");
            Teardown();
        }
        catch (Exception e)
        {
            _telemetryService.TrackException(
                e,
                new TelemetryExceptionContext(
                    Component: "Shutdown",
                    ActivityId: "app.shutdown",
                    ReasonCode: "shutdown_failed",
                    Attributes: new Dictionary<string, object?>
                    {
                        [TelemetryAttributes.CommandId] = "app.shutdown"
                    }));
            _logService.LogException(e, "Error during shutdown. Forcing exit.");
        }

        Environment.Exit(0);
    }

    private void Teardown()
    {
        IsShuttingDown = true;
        _cancellationService.CancelAll();
    }

    private void TrackShutdownRequested(string reasonCode)
    {
        _telemetryService.TrackEvent(
            TelemetryEvents.AppShutdownRequested,
            new Dictionary<string, object?>
            {
                [TelemetryAttributes.ReasonCode] = reasonCode
            });
    }
}
