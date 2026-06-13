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
            telemetryService?.ActivityInitiated(resolvedActivityId);
            await useCase.ExecuteAsync(requestFactory(), cancellationToken);
            telemetryService?.ActivityCompleted(resolvedActivityId);
        }
        catch (OperationCanceledException exception)
        {
            telemetryService?.ActivityCanceled(resolvedActivityId, exception.Message);
        }
        catch (Exception exception)
        {
            telemetryService?.ActivityError(resolvedActivityId, exception);
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
        catch (Exception exception)
        {
            telemetryService?.ActivityError(resolvedActivityId, exception, "CanExecute failed.");
            return false;
        }
    }

    private static string ResolveActivityId(object useCase, string? activityId)
    {
        return string.IsNullOrWhiteSpace(activityId)
            ? useCase.GetType().Name
            : activityId;
    }
}
