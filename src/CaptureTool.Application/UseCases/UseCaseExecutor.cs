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
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _telemetryService.ActivityCanceled(activityId);
                return UseCaseResponse<TResponse>.Cancelled();
            }

            TResponse response = await useCase(cancellationToken);

            _telemetryService.ActivityCompleted(activityId);
            return UseCaseResponse<TResponse>.Success(response);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(activityId, exception.Message);
            return UseCaseResponse<TResponse>.Cancelled();
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(activityId, exception);
            return UseCaseResponse<TResponse>.Failure();
        }
    }
}
