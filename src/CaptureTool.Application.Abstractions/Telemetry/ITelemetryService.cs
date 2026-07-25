namespace CaptureTool.Application.Abstractions.Telemetry;

/// <summary>
/// Publishes structured product-usage events.
/// </summary>
/// <remarks>
/// Implementations must be best effort and must not throw exceptions back to application code.
/// Event properties must be low-cardinality, allow-listed values and must not contain user content,
/// personal information, file paths, device names, or free-form error messages.
/// </remarks>
public interface ITelemetryService
{
    void TrackEvent(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null);
}
