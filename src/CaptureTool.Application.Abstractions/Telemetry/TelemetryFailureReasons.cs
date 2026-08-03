namespace CaptureTool.Application.Abstractions.Telemetry;

public static class TelemetryFailureReasons
{
    public const string AccessDenied = "access_denied";
    public const string ComponentUnavailable = "component_unavailable";
    public const string ConfigurationUnsupported = "configuration_unsupported";
    public const string GraphicsUnsupported = "graphics_unsupported";
    public const string InitializationFailed = "initialization_failed";
    public const string InvalidConfiguration = "invalid_configuration";
    public const string OutputUnavailable = "output_unavailable";
    public const string PlatformUnsupported = "platform_unsupported";
    public const string ResourceExhausted = "resource_exhausted";
    public const string StartTimeout = "start_timeout";
    public const string TargetUnavailable = "target_unavailable";
}
