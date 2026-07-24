namespace CaptureTool.Application.Abstractions.Telemetry;

public static class TelemetryConsentSettingValues
{
    public const string Unknown = "unknown";
    public const string Denied = "denied";
    public const string Granted = "granted";

    public static TelemetryConsentState Parse(string? value)
    {
        return value switch
        {
            Granted => TelemetryConsentState.Granted,
            Denied => TelemetryConsentState.Denied,
            _ => TelemetryConsentState.Unknown
        };
    }

    public static string Serialize(TelemetryConsentState state)
    {
        return state switch
        {
            TelemetryConsentState.Granted => Granted,
            TelemetryConsentState.Denied => Denied,
            _ => Unknown
        };
    }
}
