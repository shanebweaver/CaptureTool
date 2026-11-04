using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Windows.Shutdown;

public sealed partial class WindowsShutdownHandler : IShutdownHandler
{
    private readonly ICancellationService _cancellationService;
    private readonly ILogService _logService;
    private readonly IWindowsAppRestartService _restartService;
    private readonly ITelemetryService? _telemetryService;
    private readonly ICaptureMemoryService? _captureMemory;
    private readonly Action _exitApplication;

    public bool IsShuttingDown { get; private set; }

    public WindowsShutdownHandler(
        ILogService logService,
        ICancellationService cancellationService,
        ITelemetryService? telemetryService = null,
        ICaptureMemoryService? captureMemory = null,
        Action? exitApplication = null)
        : this(
            logService,
            cancellationService,
            new WindowsAppRestartService(),
            telemetryService, captureMemory, exitApplication)
    {
    }

    internal WindowsShutdownHandler(
        ILogService logService,
        ICancellationService cancellationService,
        IWindowsAppRestartService restartService,
        ITelemetryService? telemetryService = null,
        ICaptureMemoryService? captureMemory = null,
        Action? exitApplication = null)
    {
        _logService = logService;
        _cancellationService = cancellationService;
        _restartService = restartService;
        _telemetryService = telemetryService;
        _captureMemory = captureMemory;
        _exitApplication = exitApplication ?? (() => Microsoft.UI.Xaml.Application.Current.Exit());
    }

    public bool TryRestart()
    {
        if (IsShuttingDown)
        {
            // Can't restart, shutdown in progress.
            return false;
        }

        TrackShutdownRequested("restart");
        global::Windows.ApplicationModel.Core.AppRestartFailureReason restartError =
            _restartService.Restart(string.Empty);

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

        // Cancellation and window closure can re-enter Shutdown. Claim the
        // transition before invoking telemetry or application cleanup.
        IsShuttingDown = true;
        try
        {
            TrackShutdownRequested("exit");
            Teardown();
        }
        catch (Exception e)
        {
            _logService.LogException(e, "Error during shutdown.");
        }
        finally
        {
            // Let WinUI unwind the window callback and shut down its dispatcher.
            // Environment.Exit tears down FrameworkUdk/CoreMessaging from inside
            // Window.Closed and can fail-fast during endpoint revocation.
            _exitApplication();
        }
    }

    private void Teardown()
    {
        _captureMemory?.StopAsync().GetAwaiter().GetResult();
        _cancellationService.CancelAll();
    }

    private void TrackShutdownRequested(string source)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.AppShutdownRequested,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Source] = source
            });
    }
}
