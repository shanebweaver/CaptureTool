using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.UseCases;

internal sealed class UseCaseExecutor : IUseCaseExecutor
{
    private readonly ITelemetryService _telemetryService;

    public UseCaseExecutor(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    Task<UseCaseResponse<TResponse>> IUseCaseExecutor.ExecuteAsync<TResponse>(
        string activityId,
        Func<CancellationToken, Task<TResponse>> useCase,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(activityId, useCase, cancellationToken);
    }

    Task<UseCaseResponse<TResponse>> IUseCaseExecutor.ExecuteAsync<TResponse>(
        string activityId,
        Func<TResponse> useCase,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            activityId,
            _ =>
            {
                TResponse response = useCase();
                return Task.FromResult(response);
            },
            cancellationToken);
    }

    private async Task<UseCaseResponse<TResponse>> ExecuteAsync<TResponse>(
        string activityId,
        Func<CancellationToken, Task<TResponse>> useCase,
        CancellationToken cancellationToken)
    {
        DateTime startedUtc = DateTime.UtcNow;
        Dictionary<string, object?> attributes = new(StringComparer.Ordinal)
        {
            [TelemetryAttributes.UseCaseId] = activityId,
            [TelemetryAttributes.Component] = "UseCase"
        };

        using IDisposable? activity = _telemetryService.StartActivity(activityId, attributes);
        _telemetryService.TrackEvent(TelemetryEvents.WorkflowStarted, attributes);
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                TrackUseCaseFinished(activityId, startedUtc, "cancelled", attributes);
                _telemetryService.ActivityCanceled(activityId);
                return UseCaseResponse<TResponse>.Cancelled();
            }

            TResponse response = await useCase(cancellationToken);

            TrackUseCaseFinished(activityId, startedUtc, "success", attributes);
            _telemetryService.ActivityCompleted(activityId);
            return UseCaseResponse<TResponse>.Success(response);
        }
        catch (OperationCanceledException exception)
        {
            TrackUseCaseFinished(activityId, startedUtc, "cancelled", attributes);
            _telemetryService.ActivityCanceled(activityId, exception.Message);
            return UseCaseResponse<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            Dictionary<string, object?> failureAttributes = new(attributes, StringComparer.Ordinal)
            {
                [TelemetryAttributes.Outcome] = "failed"
            };
            _telemetryService.TrackException(
                exception,
                new TelemetryExceptionContext(
                    Component: "UseCase",
                    ActivityId: activityId,
                    UseCaseId: activityId,
                    Attributes: failureAttributes));
            TrackUseCaseFinished(activityId, startedUtc, "failed", attributes);
            _telemetryService.ActivityError(activityId, exception);
            return UseCaseResponse<TResponse>.Failure();
        }
    }

    private void TrackUseCaseFinished(
        string activityId,
        DateTime startedUtc,
        string outcome,
        IReadOnlyDictionary<string, object?> baseAttributes)
    {
        double durationMs = (DateTime.UtcNow - startedUtc).TotalMilliseconds;
        Dictionary<string, object?> attributes = new(baseAttributes, StringComparer.Ordinal)
        {
            [TelemetryAttributes.Outcome] = outcome,
            [TelemetryAttributes.DurationMs] = durationMs
        };

        string eventName = outcome switch
        {
            "success" => TelemetryEvents.WorkflowCompleted,
            "cancelled" => TelemetryEvents.WorkflowCancelled,
            _ => TelemetryEvents.WorkflowFailed
        };

        _telemetryService.TrackEvent(eventName, attributes);
        _telemetryService.TrackMetric("use_case.duration_ms", durationMs, attributes);
    }
}
