using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using System.Runtime.CompilerServices;
using System.Text;

namespace CaptureTool.Infrastructure.Telemetry;

public sealed partial class TelemetryService : ITelemetryService
{
    private readonly ILogService _logService;
    private readonly ITelemetryContext? _telemetryContext;
    private readonly ITelemetrySanitizer _sanitizer;
    private readonly TelemetryRuntime? _telemetryRuntime;

    public TelemetryService(
        ILogService logService,
        ITelemetryContext? telemetryContext = null,
        ITelemetrySanitizer? sanitizer = null,
        TelemetryRuntime? telemetryRuntime = null)
    {
        _logService = logService;
        _telemetryContext = telemetryContext;
        _sanitizer = sanitizer ?? new TelemetrySanitizer();
        _telemetryRuntime = telemetryRuntime;
    }

    public IDisposable? StartActivity(string name, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        if (!IsRemoteTelemetryEnabled())
        {
            return null;
        }

        IReadOnlyDictionary<string, object?> sanitizedAttributes = MergeAndSanitize(attributes);
        return new LocalTelemetryActivityScope(this, name, sanitizedAttributes, _telemetryRuntime?.StartActivity(name, sanitizedAttributes));
    }

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        if (!IsRemoteTelemetryEnabled())
        {
            return;
        }

        string sanitizedEventName = _sanitizer.SanitizeEventName(eventName);
        IReadOnlyDictionary<string, object?> sanitizedAttributes = MergeAndSanitize(attributes);
        _telemetryRuntime?.TrackEvent(sanitizedEventName, sanitizedAttributes);
        _logService.LogInformation($"Telemetry event: {sanitizedEventName}{FormatAttributes(sanitizedAttributes)}");
    }

    public void TrackException(Exception exception, TelemetryExceptionContext context)
    {
        if (!IsRemoteTelemetryEnabled())
        {
            return;
        }

        Dictionary<string, object?> attributes = new(context.Attributes ?? new Dictionary<string, object?>(), StringComparer.Ordinal)
        {
            [TelemetryAttributes.Component] = context.Component,
            [TelemetryAttributes.ExceptionType] = exception.GetType().Name,
            [TelemetryAttributes.Fatal] = context.Fatal
        };

        if (!string.IsNullOrWhiteSpace(context.ActivityId))
        {
            attributes[TelemetryAttributes.ActivityId] = context.ActivityId;
        }

        if (!string.IsNullOrWhiteSpace(context.UseCaseId))
        {
            attributes[TelemetryAttributes.UseCaseId] = context.UseCaseId;
        }

        if (!string.IsNullOrWhiteSpace(context.Route))
        {
            attributes[TelemetryAttributes.CurrentRoute] = context.Route;
        }

        if (!string.IsNullOrWhiteSpace(context.ReasonCode))
        {
            attributes[TelemetryAttributes.ReasonCode] = context.ReasonCode;
        }

        IReadOnlyDictionary<string, object?> sanitizedAttributes = MergeAndSanitize(attributes);
        _telemetryRuntime?.TrackException(exception, sanitizedAttributes);
        _logService.LogInformation($"Telemetry exception: {exception.GetType().Name}{FormatAttributes(sanitizedAttributes)}");
    }

    public void TrackMetric(string metricName, double value, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        if (!IsRemoteTelemetryEnabled())
        {
            return;
        }

        IReadOnlyDictionary<string, object?> sanitizedAttributes = MergeAndSanitize(attributes);
        _telemetryRuntime?.TrackMetric(metricName, value, sanitizedAttributes);
        _logService.LogInformation($"Telemetry metric: {metricName}={value}{FormatAttributes(sanitizedAttributes)}");
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

    private bool IsRemoteTelemetryEnabled()
    {
        return _telemetryContext?.IsTelemetryEnabled == true;
    }

    private IReadOnlyDictionary<string, object?> MergeAndSanitize(IReadOnlyDictionary<string, object?>? attributes)
    {
        Dictionary<string, object?> mergedAttributes = new(StringComparer.Ordinal);
        if (_telemetryContext is not null)
        {
            foreach ((string key, object? value) in _telemetryContext.GetGlobalAttributes())
            {
                mergedAttributes[key] = value;
            }
        }

        if (attributes is not null)
        {
            foreach ((string key, object? value) in attributes)
            {
                mergedAttributes[key] = value;
            }
        }

        return _sanitizer.SanitizeAttributes(mergedAttributes);
    }

    private static string FormatAttributes(IReadOnlyDictionary<string, object?> attributes)
    {
        if (attributes.Count == 0)
        {
            return string.Empty;
        }

        return " - " + string.Join(", ", attributes.Select(attribute => $"{attribute.Key}={attribute.Value}"));
    }

    private sealed class LocalTelemetryActivityScope : IDisposable
    {
        private readonly TelemetryService _telemetryService;
        private readonly string _name;
        private readonly IReadOnlyDictionary<string, object?> _attributes;
        private readonly IDisposable? _activity;
        private readonly DateTime _startedUtc = DateTime.UtcNow;
        private bool _disposed;

        public LocalTelemetryActivityScope(
            TelemetryService telemetryService,
            string name,
            IReadOnlyDictionary<string, object?> attributes,
            IDisposable? activity)
        {
            _telemetryService = telemetryService;
            _name = name;
            _attributes = attributes;
            _activity = activity;
            _telemetryService._logService.LogInformation($"Telemetry activity started: {_name}{FormatAttributes(_attributes)}");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            double durationMs = (DateTime.UtcNow - _startedUtc).TotalMilliseconds;
            Dictionary<string, object?> completedAttributes = new(_attributes, StringComparer.Ordinal)
            {
                [TelemetryAttributes.DurationMs] = durationMs
            };
            _telemetryService._logService.LogInformation($"Telemetry activity completed: {_name}{FormatAttributes(completedAttributes)}");
            _activity?.Dispose();
            _disposed = true;
        }
    }
}
