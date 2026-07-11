using System.Runtime.CompilerServices;

namespace CaptureTool.Application.Abstractions.Telemetry;

public interface ITelemetryService
{
    IDisposable? StartActivity(string name, IReadOnlyDictionary<string, object?>? attributes = null);
    void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? attributes = null);
    void TrackException(Exception exception, TelemetryExceptionContext context);
    void TrackMetric(string metricName, double value, IReadOnlyDictionary<string, object?>? attributes = null);

    void ActivityInitiated(string activityId, string? message = null);
    void ActivityCompleted(string activityId, string? message = null);
    void ActivityCanceled(string activityId, string? message = null);
    void ActivityError(
        string activityId,
        Exception exception,
        string? message = null,
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerArgumentExpression(nameof(exception))] string? exceptionExpr = null);
    void ButtonInvoked(string buttonId, string? message);
}
