using CaptureTool.Application.Abstractions.UseCases;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Shared.Commands;

internal static class UseCaseCommandExtensions
{
    public static IRelayCommand ToRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TRequest> requestFactory)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand(cancellationToken => useCase.ExecuteAsync(requestFactory(), cancellationToken));
    }

    public static IRelayCommand ToRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        IConditional<TRequest> conditional,
        Func<TRequest> requestFactory)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(conditional);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand(
            cancelableExecute: cancellationToken => useCase.ExecuteAsync(requestFactory(), cancellationToken),
            canExecute: () => conditional.CanExecute(requestFactory()));
    }

    public static IRelayCommand<TParameter> ToRelayCommand<TParameter, TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TParameter, TRequest> requestFactory)
        where TParameter : notnull
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand<TParameter>(async (parameter, cancellationToken) =>
        {
            if (parameter is null)
            {
                return;
            }

            await useCase.ExecuteAsync(requestFactory(parameter), cancellationToken);
        });
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TRequest> requestFactory)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand(cancellationToken => useCase.ExecuteAsync(requestFactory(), cancellationToken));
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        IConditional<TRequest> conditional,
        Func<TRequest> requestFactory)
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(conditional);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand(
            cancelableExecute: cancellationToken => useCase.ExecuteAsync(requestFactory(), cancellationToken),
            canExecute: () => conditional.CanExecute(requestFactory()));
    }

    public static IAsyncRelayCommand<TParameter> ToAsyncRelayCommand<TParameter, TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> useCase,
        Func<TParameter, TRequest> requestFactory)
        where TParameter : notnull
    {
        ArgumentNullException.ThrowIfNull(useCase);
        ArgumentNullException.ThrowIfNull(requestFactory);

        return new AsyncRelayCommand<TParameter>(async (parameter, cancellationToken) =>
        {
            if (parameter is null)
            {
                return;
            }

            await useCase.ExecuteAsync(requestFactory(parameter), cancellationToken);
        });
    }
}
