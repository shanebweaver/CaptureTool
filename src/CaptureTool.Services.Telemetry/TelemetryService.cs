using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace CaptureTool.Services.Telemetry;

public sealed partial class TelemetryService : ITelemetryService
{
    public void ActivityInitiated(string activityId, string? message = null)
    {
        StringBuilder stringBuilder = new($"Activity initiated: {activityId}");
        if (message != null)
        {
            stringBuilder.Append(message);
        }

        Debug.WriteLine(stringBuilder.ToString());
    }

    public void ActivityCanceled(string activityId, string? message = null)
    {
        StringBuilder stringBuilder = new($"Activity canceled: {activityId}");
        if (message != null)
        {
            stringBuilder.Append(message);
        }

        Debug.WriteLine(stringBuilder.ToString());
    }

    public void ActivityCompleted(string activityId, string? message = null)
    {
        StringBuilder stringBuilder = new($"Activity completed: {activityId}");
        if (message != null)
        {
            stringBuilder.Append(message);
        }

        Debug.WriteLine(stringBuilder.ToString());
    }

    public void ActivityError(string activityId, string message, [CallerMemberName] string? callerName = null)
    {
        Debug.WriteLine($"Activity error: {activityId} - {callerName} - {message}");
    }
}