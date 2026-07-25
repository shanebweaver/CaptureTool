using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.UseCases;

internal sealed class UseCaseExecutor : IUseCaseExecutor
{
    private readonly ILogService _logService;
    private readonly ITelemetryService _telemetryService;

    public UseCaseExecutor(
        ILogService logService,
        ITelemetryService telemetryService)
    {
        _logService = logService;
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
        _logService.LogInformation($"Activity initiated: {activityId}");

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logService.LogInformation($"Activity canceled: {activityId}");
                TrackAction(activityId, TelemetryOutcomes.Canceled);
                return UseCaseResponse<TResponse>.Cancelled();
            }

            TResponse response = await useCase(cancellationToken);

            _logService.LogInformation($"Activity completed: {activityId}");
            TrackAction(activityId, TelemetryOutcomes.Succeeded);
            return UseCaseResponse<TResponse>.Success(response);
        }
        catch (OperationCanceledException exception)
        {
            _logService.LogInformation($"Activity canceled: {activityId} - Message: {exception.Message}");
            TrackAction(activityId, TelemetryOutcomes.Canceled);
            return UseCaseResponse<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            _logService.LogException(exception, $"Activity error: {activityId}");
            TrackAction(activityId, TelemetryOutcomes.Failed);
            return UseCaseResponse<TResponse>.Failure();
        }
    }

    private void TrackAction(string activityId, string outcome)
    {
        _telemetryService.TrackEvent(
            TelemetryEvents.UseCaseCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Action] = activityId,
                [TelemetryProperties.Outcome] = outcome
            });
    }
}
