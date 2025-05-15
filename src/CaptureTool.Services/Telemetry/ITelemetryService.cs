using System;
using System.Runtime.CompilerServices;

namespace CaptureTool.Services.Telemetry;

public interface ITelemetryService
{
    void ActivityInitiated(string activityId, string? message = null);
    void ActivityCompleted(string activityId, string? message = null);
    void ActivityCanceled(string activityId, string? message = null);
    void ActivityError(string activityId, Exception exception, string? message = null, [CallerMemberName] string? callerName = null);
}
