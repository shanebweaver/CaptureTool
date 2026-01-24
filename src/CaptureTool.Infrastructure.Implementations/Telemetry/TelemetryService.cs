using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using System.Runtime.CompilerServices;
using System.Text;

namespace CaptureTool.Infrastructure.Implementations.Telemetry;

public sealed partial class TelemetryService : ITelemetryService
{
    private readonly ILogService _logService;

    public TelemetryService(ILogService logService)
    {
        _logService = logService;
    }

    public void ActivityInitiated(string activityId, string? message = null)
    {
        StringBuilder stringBuilder = new($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Activity initiated: {activityId}");
        if (message != null)
        {
            stringBuilder.Append($" - Message: {message}");
        }

        _logService.LogInformation(stringBuilder.ToString());
    }

    public void ActivityCanceled(string activityId, string? message = null)
    {
        StringBuilder stringBuilder = new($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Activity canceled: {activityId}");
        if (message != null)
        {
            stringBuilder.Append($" - Message: {message}");
        }

        _logService.LogInformation(stringBuilder.ToString());
    }

    public void ActivityCompleted(string activityId, string? message = null)
    {
        StringBuilder stringBuilder = new($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Activity completed: {activityId}");
        if (message != null)
        {
            stringBuilder.Append($" - Message: {message}");
        }

        _logService.LogInformation(stringBuilder.ToString());
    }

    public void ActivityError(
        string activityId,
        Exception exception,
        string? message = null,
        [CallerMemberName] string? caller = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerArgumentExpression(nameof(exception))] string? exceptionExpr = null)
    {
        var sb = new StringBuilder($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Activity error: {activityId}");

        sb.Append($" - Caller: {caller}");
        sb.Append($" - Line: {line}");

        if (file != null)
        {
            sb.Append($" - File: {Path.GetFileName(file)}");
        }

        if (exceptionExpr != null)
        {
            sb.Append($" - Exception Expr: {exceptionExpr}");
        }

        if (message != null)
        {
            sb.Append($" - Message: {message}");
        }

        _logService.LogException(exception, sb.ToString());
    }

    public void ButtonInvoked(string buttonId, string? message)
    {
        StringBuilder stringBuilder = new($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Button invoked: {buttonId}");
        if (message != null)
        {
            stringBuilder.Append(message);
        }

        _logService.LogInformation(stringBuilder.ToString());
    }
}