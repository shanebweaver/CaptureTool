using CaptureTool.Application.Abstractions.Telemetry;

namespace CaptureTool.Infrastructure.Tests.Navigation;

internal sealed class RecordingTelemetry : ITelemetryService
{
    public List<(string EventName, IReadOnlyDictionary<string, object?> Attributes)> Events { get; } = [];
    public List<(string MetricName, double Value, IReadOnlyDictionary<string, object?> Attributes)> Metrics { get; } = [];

    public IDisposable? StartActivity(string name, IReadOnlyDictionary<string, object?>? attributes = null) => null;

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        Events.Add((eventName, attributes ?? new Dictionary<string, object?>()));
    }

    public void TrackException(Exception exception, TelemetryExceptionContext context)
    {
    }

    public void TrackMetric(string metricName, double value, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        Metrics.Add((metricName, value, attributes ?? new Dictionary<string, object?>()));
    }

    public void ActivityInitiated(string activityId, string? message = null)
    {
    }

    public void ActivityCompleted(string activityId, string? message = null)
    {
    }

    public void ActivityCanceled(string activityId, string? message = null)
    {
    }

    public void ActivityError(
        string activityId,
        Exception exception,
        string? message = null,
        string? caller = null,
        string? file = null,
        int line = 0,
        string? exceptionExpr = null)
    {
    }

    public void ButtonInvoked(string buttonId, string? message)
    {
    }
}
