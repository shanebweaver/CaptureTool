namespace CaptureTool.Application.Abstractions.Telemetry;

/// <summary>
/// Receives telemetry events after consent has been granted.
/// </summary>
public interface ITelemetryEventSink
{
    void TrackEvent(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null);
}
