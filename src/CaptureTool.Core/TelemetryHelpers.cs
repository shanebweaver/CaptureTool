using CaptureTool.Services.Telemetry;
using System;
using System.Threading.Tasks;

namespace CaptureTool.Core;

public static class TelemetryHelpers
{
    public static void ExecuteActivity(ITelemetryService telemetryService, string activityId, Action activityAction)
    {
        telemetryService.ActivityInitiated(activityId);

        try
        {
            activityAction();
            telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            telemetryService.ActivityCanceled(activityId);
        }
        catch (Exception e)
        {
            telemetryService.ActivityError(activityId, e);
        }
    }

    public static async Task ExecuteActivityAsync(ITelemetryService telemetryService, string activityId, Func<Task> action)
    {
        telemetryService.ActivityInitiated(activityId);

        try
        {
            await action();
            telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            telemetryService.ActivityCanceled(activityId);
        }
        catch (Exception e)
        {
            telemetryService.ActivityError(activityId, e);
        }
    }
}
