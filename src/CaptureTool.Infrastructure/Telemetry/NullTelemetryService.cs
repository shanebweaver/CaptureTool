using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Telemetry;

/// <summary>
/// Discards all telemetry events.
/// </summary>
public sealed class NullTelemetryService : ITelemetryEventSink
{
    public void TrackEvent(
        string eventName,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
    }
}
