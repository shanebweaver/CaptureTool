using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

    public void ActivityError(string activityId, Exception exception, string? message = null, [CallerMemberName] string? callerName = null)
    {
        StringBuilder stringBuilder = new($"Activity error: {activityId}");
        if (callerName != null)
        {
            stringBuilder.Append($"- Caller: {callerName}");
        }
        if (message != null)
        {
            stringBuilder.Append($"- Message: {message}");
        }
        stringBuilder.Append($"- Exception: {exception.Message}");

        Debug.WriteLine(stringBuilder.ToString());
    }

    public void ExecuteActivity(string activityId, Action activityAction)
    {
        ActivityInitiated(activityId);

        try
        {
            activityAction();
            ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            ActivityCanceled(activityId);
            throw;
        }
        catch (Exception e)
        {
            ActivityError(activityId, e);
            throw;
        }
    }

    public async Task ExecuteActivityAsync(string activityId, Func<Task> action)
    {
        ActivityInitiated(activityId);

        try
        {
            await action();
            ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            ActivityCanceled(activityId);
            throw;
        }
        catch (Exception e)
        {
            ActivityError(activityId, e);
            throw;
        }
    }


    public void ButtonInvoked(string buttonId, string? message)
    {
        StringBuilder stringBuilder = new($"Button invoked: {buttonId}");
        if (message != null)
        {
            stringBuilder.Append(message);
        }

        Debug.WriteLine(stringBuilder.ToString());
    }
}