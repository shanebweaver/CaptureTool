namespace CaptureTool.Application.Abstractions.Telemetry;

public static class TelemetryAttributes
{
    public const string AppName = "app.name";
    public const string AppVersion = "app.version";
    public const string AppBuildChannel = "app.build_channel";
    public const string SessionId = "session.id";
    public const string InstallIdHash = "install.id_hash";
    public const string CurrentRoute = "route.current";
    public const string SchemaVersion = "telemetry.schema_version";

    public const string ActivityId = "activity.id";
    public const string ActivationKind = "activation.kind";
    public const string CommandId = "command.id";
    public const string Component = "component";
    public const string CaptureMode = "capture.mode";
    public const string CaptureType = "capture.type";
    public const string DurationMs = "duration_ms";
    public const string EventName = "microsoft.custom_event.name";
    public const string ExceptionType = "exception.type";
    public const string Fatal = "fatal";
    public const string FromRoute = "from_route";
    public const string MediaType = "media.type";
    public const string Outcome = "outcome";
    public const string ParameterType = "parameter_type";
    public const string ReasonCode = "reason_code";
    public const string SettingKey = "setting.key";
    public const string StoreStatus = "store.status";
    public const string Surface = "surface";
    public const string ToRoute = "to_route";
    public const string UseCaseId = "use_case.id";
}
