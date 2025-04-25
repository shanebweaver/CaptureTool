using System.Runtime.CompilerServices;

namespace CaptureTool.Services.Telemetry;

public interface ITelemetryService
{
    void ActivityInitiated(string activityId, string? message = null);
    void ActivityCompleted(string activityId, string? message = null);
    void ActivityCanceled(string activityId, string? message = null);
    void ActivityError(string activityId, string message, [CallerMemberName] string? callerName = null);
}
