using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shared.Commands;

internal static class UseCaseCommandExtensions
{
    public static IRelayCommand ToRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TRequest> requestFactory,
        ITelemetryService? telemetryService = null,
        string? activityId = null)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return useCase is IConditional<TRequest> conditional
            ? new AsyncRelayCommand(
                cancelableExecute: cancellationToken => ExecuteUseCaseAsync(useCase, requestFactory, telemetryService, activityId, cancellationToken),
                canExecute: () => CanExecute(conditional, requestFactory, telemetryService, activityId))
            : new AsyncRelayCommand(cancellationToken => ExecuteUseCaseAsync(useCase, requestFactory, telemetryService, activityId, cancellationToken));
    }

    public static IRelayCommand ToRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        IConditional<TRequest> conditional,
        Func<TRequest> requestFactory,
        ITelemetryService? telemetryService = null,
        string? activityId = null)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(conditional);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand(
            cancelableExecute: cancellationToken => ExecuteUseCaseAsync(useCase, requestFactory, telemetryService, activityId, cancellationToken),
            canExecute: () => CanExecute(conditional, requestFactory, telemetryService, activityId));
    }

    public static IRelayCommand<TParameter> ToRelayCommand<TParameter, TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TParameter, TRequest> requestFactory,
        ITelemetryService? telemetryService = null,
        string? activityId = null)
        where TParameter : notnull
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand<TParameter>(async (parameter, cancellationToken) =>
        {
            await ExecuteUseCaseAsync(useCase, parameter, requestFactory, telemetryService, activityId, cancellationToken);
        });
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TRequest> requestFactory,
        ITelemetryService? telemetryService = null,
        string? activityId = null)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return useCase is IConditional<TRequest> conditional
            ? new AsyncRelayCommand(
                cancelableExecute: cancellationToken => ExecuteUseCaseAsync(useCase, requestFactory, telemetryService, activityId, cancellationToken),
                canExecute: () => CanExecute(conditional, requestFactory, telemetryService, activityId))
            : new AsyncRelayCommand(cancellationToken => ExecuteUseCaseAsync(useCase, requestFactory, telemetryService, activityId, cancellationToken));
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        IConditional<TRequest> conditional,
        Func<TRequest> requestFactory,
        ITelemetryService? telemetryService = null,
        string? activityId = null)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(conditional);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand(
            cancelableExecute: cancellationToken => ExecuteUseCaseAsync(useCase, requestFactory, telemetryService, activityId, cancellationToken),
            canExecute: () => CanExecute(conditional, requestFactory, telemetryService, activityId));
    }

    public static IAsyncRelayCommand<TParameter> ToAsyncRelayCommand<TParameter, TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TParameter, TRequest> requestFactory,
        ITelemetryService? telemetryService = null,
        string? activityId = null)
        where TParameter : notnull
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand<TParameter>(async (parameter, cancellationToken) =>
        {
            await ExecuteUseCaseAsync(useCase, parameter, requestFactory, telemetryService, activityId, cancellationToken);
        });
    }

    private static async Task ExecuteUseCaseAsync<TRequest, TResponse>(
        IUseCase<TRequest, TResponse> useCase,
        Func<TRequest> requestFactory,
        ITelemetryService? telemetryService,
        string? activityId,
        CancellationToken cancellationToken)
    {
        string resolvedActivityId = ResolveActivityId(useCase, activityId);

        try
        {
            UseCaseResponse<TResponse> response = await useCase.ExecuteAsync(requestFactory(), cancellationToken);
            TrackAction(telemetryService, resolvedActivityId, ResolveOutcome(response.Result));
        }
        catch (OperationCanceledException)
        {
            TrackAction(telemetryService, resolvedActivityId, TelemetryOutcomes.Canceled);
        }
        catch (Exception)
        {
            TrackAction(telemetryService, resolvedActivityId, TelemetryOutcomes.Failed);
        }
    }

    private static async Task ExecuteUseCaseAsync<TParameter, TRequest, TResponse>(
        IUseCase<TRequest, TResponse> useCase,
        TParameter? parameter,
        Func<TParameter, TRequest> requestFactory,
        ITelemetryService? telemetryService,
        string? activityId,
        CancellationToken cancellationToken)
        where TParameter : notnull
    {
        if (parameter is null)
        {
            return;
        }

        await ExecuteUseCaseAsync(useCase, () => requestFactory(parameter), telemetryService, activityId, cancellationToken);
    }

    private static bool CanExecute<TRequest>(
        IConditional<TRequest> conditional,
        Func<TRequest> requestFactory,
        ITelemetryService? telemetryService,
        string? activityId)
    {
        string resolvedActivityId = ResolveActivityId(conditional, activityId);

        try
        {
            return conditional.CanExecute(requestFactory());
        }
        catch (Exception)
        {
            TrackAction(telemetryService, resolvedActivityId, TelemetryOutcomes.Failed);
            return false;
        }
    }

    private static void TrackAction(
        ITelemetryService? telemetryService,
        string activityId,
        string outcome)
    {
        telemetryService?.TrackEvent(
            TelemetryEvents.UserAction,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Action] = activityId,
                [TelemetryProperties.Outcome] = outcome
            });
    }

    private static string ResolveOutcome(UseCaseResult result)
    {
        return result switch
        {
            UseCaseResult.Succeeded => TelemetryOutcomes.Succeeded,
            UseCaseResult.Cancelled => TelemetryOutcomes.Canceled,
            UseCaseResult.Failed => TelemetryOutcomes.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
        };
    }

    private static string ResolveActivityId(object useCase, string? activityId)
    {
        return string.IsNullOrWhiteSpace(activityId)
            ? useCase.GetType().Name
            : activityId;
    }
}
