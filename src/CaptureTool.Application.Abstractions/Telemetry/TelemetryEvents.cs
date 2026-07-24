namespace CaptureTool.Application.Abstractions.Telemetry;

public static class TelemetryEvents
{
    public const string AppActivated = "app.activated";
    public const string AppShutdownRequested = "app.shutdown_requested";
    public const string AppStarted = "app.started";
    public const string CaptureCanceled = "capture.canceled";
    public const string CaptureCompleted = "capture.completed";
    public const string CaptureFailed = "capture.failed";
    public const string CaptureRequested = "capture.requested";
    public const string CaptureStarted = "capture.started";
    public const string DiagnosticsAction = "diagnostics.action";
    public const string EditorOpened = "editor.opened";
    public const string EditToolInvoked = "edit.tool_invoked";
    public const string FeedbackOpened = "feedback.opened";
    public const string NavigationCompleted = "navigation.completed";
    public const string OutputCompleted = "output.completed";
    public const string SettingsChanged = "settings.changed";
    public const string StoreOpened = "store.opened";
    public const string StorePurchaseCompleted = "store.purchase_completed";
    public const string StorePurchaseStarted = "store.purchase_started";
    public const string UiCommandCompleted = "ui.command_completed";
    public const string UiCommandInvoked = "ui.command_invoked";
    public const string UseCaseCompleted = "use_case.completed";
    public const string UserAction = "user.action";
}
