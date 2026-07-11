namespace CaptureTool.Application.Abstractions.Telemetry;

public interface ITelemetrySanitizer
{
    string SanitizeEventName(string eventName);
    IReadOnlyDictionary<string, object?> SanitizeAttributes(IReadOnlyDictionary<string, object?>? attributes);
}
