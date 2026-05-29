using CaptureTool.Application.Abstractions;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

internal static class CommandAdapters
{
    public static IRelayCommand ToRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> command,
        Func<TRequest> requestFactory)
    {
        return new RelayCommand(() => command.ExecuteAsync(requestFactory()).GetAwaiter().GetResult());
    }

    public static IRelayCommand ToRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> command,
        IConditional<TRequest> conditional,
        Func<TRequest> requestFactory)
    {
        return new RelayCommand(
            execute: () => command.ExecuteAsync(requestFactory()).GetAwaiter().GetResult(),
            canExecute: () => conditional.CanExecuteAsync(requestFactory()).GetAwaiter().GetResult());
    }

    public static IRelayCommand<TParameter> ToRelayCommand<TParameter, TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> command,
        Func<TParameter, TRequest> requestFactory)
        where TParameter : notnull
    {
        return new RelayCommand<TParameter>(parameter =>
        {
            if (parameter is null)
            {
                return;
            }

            command.ExecuteAsync(requestFactory(parameter)).GetAwaiter().GetResult();
        });
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> command,
        Func<TRequest> requestFactory)
    {
        return new AsyncRelayCommand(cancellationToken => command.ExecuteAsync(requestFactory(), cancellationToken));
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand<TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> command,
        IConditional<TRequest> conditional,
        Func<TRequest> requestFactory)
    {
        return new AsyncRelayCommand(
            cancelableExecute: cancellationToken => command.ExecuteAsync(requestFactory(), cancellationToken),
            canExecute: () => conditional.CanExecuteAsync(requestFactory()).GetAwaiter().GetResult());
    }

    public static IAsyncRelayCommand<TParameter> ToAsyncRelayCommand<TParameter, TRequest, TResponse>(
        this IUseCase<TRequest, TResponse> command,
        Func<TParameter, TRequest> requestFactory)
        where TParameter : notnull
    {
        return new AsyncRelayCommand<TParameter>(async (parameter, cancellationToken) =>
        {
            if (parameter is null)
            {
                return;
            }

            await command.ExecuteAsync(requestFactory(parameter), cancellationToken);
        });
    }
}