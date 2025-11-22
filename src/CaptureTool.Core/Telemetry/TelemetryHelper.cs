using CaptureTool.Services.Interfaces.Telemetry;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CaptureTool.Core.Telemetry;

public static class TelemetryHelper
{
    public static void ExecuteActivity(
        ITelemetryService telemetryService, 
        string activityId, 
        Action activityAction,
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerArgumentExpression(nameof(activityAction))] string? actionExpr = null)
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
            var frame = GetRootUserFrame(e);

            telemetryService.ActivityError(
                activityId, 
                e,
                $"Thrown in: {frame.method} ({frame.file}:{frame.line})", 
                caller, 
                file, 
                line,
                actionExpr);
        }
    }

    public static async Task ExecuteActivityAsync(
        ITelemetryService telemetryService, 
        string activityId, 
        Func<Task> action,
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerArgumentExpression(nameof(action))] string? actionExpr = null)
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
            var frame = GetRootUserFrame(e);

            telemetryService.ActivityError(
                activityId,
                e,
                $"Thrown in: {frame.method} ({frame.file}:{frame.line})",
                caller,
                file,
                line,
                actionExpr);
        }
    }

    private static Exception Unwrap(Exception ex)
    {
        // Async: Task.WhenAll or Task.Run
        if (ex is AggregateException agg && agg.InnerExceptions.Count == 1)
            return Unwrap(agg.InnerExceptions[0]);

        // Reflection-based wrappers
        if (ex is TargetInvocationException tie && tie.InnerException != null)
            return Unwrap(tie.InnerException);

        // The exception that actually caused the failure
        return ex;
    }

    private static (string method, string file, int line) GetRootUserFrame(Exception ex)
    {
        var actual = Unwrap(ex);

        var stack = actual.StackTrace;
        if (string.IsNullOrEmpty(stack))
            return ("<unknown>", "?", 0);

        var lines = stack.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip helper
            if (trimmed.Contains(nameof(TelemetryHelper)))
                continue;

            // Skip framework
            if (trimmed.Contains("System.") || trimmed.Contains("Microsoft."))
                continue;

            // Skip async state machine frames
            if (trimmed.Contains("MoveNext"))
                continue;

            // Skip lambdas
            if (trimmed.Contains("<<"))
                continue;

            // Example format:
            // at CaptureTool.Services.VideoService.LoadVideo() in C:\src\...\VideoService.cs:line 73

            // Extract method
            var methodStart = trimmed.IndexOf("at ") + 3;
            var methodEnd = trimmed.IndexOf(" in ");
            var methodName = methodEnd > methodStart
                ? trimmed[methodStart..methodEnd]
                : trimmed[methodStart..];

            // Extract file + line if available
            string file = "?";
            int lineNum = 0;

            var fileIndex = trimmed.IndexOf(":line ");
            if (fileIndex > 0)
            {
                var start = trimmed.LastIndexOf('\\', fileIndex);
                if (start > 0)
                    file = trimmed[(start + 1)..fileIndex];

                if (int.TryParse(trimmed[(fileIndex + 6)..].Trim(), out var ln))
                    lineNum = ln;
            }

            return (methodName.Trim(), file, lineNum);
        }

        return ("<unknown>", "?", 0);
    }

}
