using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Services.Telemetry;

public interface ITelemetryService
{
    void ActivityInitiated(string activityId, string? message = null);
    void ActivityCompleted(string activityId, string? message = null);
    void ActivityCanceled(string activityId, string? message = null);
    void ActivityError(string activityId, Exception exception, string? message = null, [CallerMemberName] string? callerName = null);
    void ExecuteActivity(string activityId, Action activityAction);
    Task ExecuteActivityAsync(string activityId, Func<Task> activityTaskFunc);

    void ButtonInvoked(string buttonId, string? message);
}
