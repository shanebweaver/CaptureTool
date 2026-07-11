namespace CaptureTool.Application.Abstractions.Telemetry;

public static class TelemetryEvents
{
    public const string AppStarted = "app.started";
    public const string AppActivated = "app.activated";
    public const string AppExited = "app.exited";
    public const string AppShutdownRequested = "app.shutdown_requested";
    public const string CaptureCancelled = "capture.cancelled";
    public const string CaptureCompleted = "capture.completed";
    public const string CaptureFailed = "capture.failed";
    public const string CaptureStarted = "capture.started";
    public const string DiagnosticsActionInvoked = "diagnostics.action.invoked";
    public const string EditCommandInvoked = "edit.command.invoked";
    public const string ExceptionCaptured = "exception.captured";
    public const string FileOpened = "file.opened";
    public const string FileSaved = "file.saved";
    public const string NavigationCompleted = "navigation.completed";
    public const string SettingsChanged = "settings.changed";
    public const string ShareInvoked = "share.invoked";
    public const string StorePurchaseCompleted = "store.purchase.completed";
    public const string StorePurchaseStarted = "store.purchase.started";
    public const string UiCommandInvoked = "ui.command.invoked";
    public const string WorkflowCancelled = "workflow.cancelled";
    public const string WorkflowCompleted = "workflow.completed";
    public const string WorkflowFailed = "workflow.failed";
    public const string WorkflowStarted = "workflow.started";

    public const string Unknown = "telemetry.unknown_event";

    public static readonly IReadOnlySet<string> KnownEventNames = new HashSet<string>(StringComparer.Ordinal)
    {
        AppStarted,
        AppActivated,
        AppExited,
        AppShutdownRequested,
        CaptureCancelled,
        CaptureCompleted,
        CaptureFailed,
        CaptureStarted,
        DiagnosticsActionInvoked,
        EditCommandInvoked,
        ExceptionCaptured,
        FileOpened,
        FileSaved,
        NavigationCompleted,
        SettingsChanged,
        ShareInvoked,
        StorePurchaseCompleted,
        StorePurchaseStarted,
        UiCommandInvoked,
        WorkflowCancelled,
        WorkflowCompleted,
        WorkflowFailed,
        WorkflowStarted
    };
}
