using System.Runtime.CompilerServices;

namespace CaptureTool.Services.Interfaces.Telemetry;

public interface ITelemetryService
{
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
