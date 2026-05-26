using CaptureTool.Infrastructure.Abstractions.Commands;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

internal static class AppCommandAdapters
{
    public static IRelayCommand ToRelayCommand(this IAppCommand appCommand)
    {
        return new RelayCommand(appCommand.Execute);
    }

    public static IRelayCommand ToRelayCommand(this IConditionalAppCommand appCommand)
    {
        return new RelayCommand(
            execute: appCommand.Execute,
            canExecute: appCommand.CanExecute);
    }

    public static IRelayCommand<T> ToRelayCommand<T>(this IAppCommand<T> appCommand) where T : notnull
    {
        return new RelayCommand<T>(
            parameter =>
            {
                if (parameter is null)
                {
                    return;
                }

                appCommand.Execute(parameter);
            });
    }

    public static IRelayCommand<T> ToRelayCommand<T>(this IConditionalAppCommand<T> appCommand) where T : notnull
    {
        return new RelayCommand<T>(
            execute: parameter =>
            {
                if (parameter is null)
                {
                    return;
                }

                appCommand.Execute(parameter);
            },
            canExecute: parameter =>
            {
                return parameter is not null
                    && appCommand.CanExecute(parameter);
            });
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand(this IAsyncAppCommand appCommand)
    {
        return new AsyncRelayCommand(appCommand.ExecuteAsync);
    }

    public static IAsyncRelayCommand ToAsyncRelayCommand(this IAsyncConditionalAppCommand appCommand)
    {
        return new AsyncRelayCommand(
            cancelableExecute: appCommand.ExecuteAsync,
            canExecute: appCommand.CanExecute);
    }

    public static IAsyncRelayCommand<T> ToAsyncRelayCommand<T>(this IAsyncAppCommand<T> appCommand) where T : notnull
    {
        return new AsyncRelayCommand<T>(
            async (parameter, cancellationToken) =>
            {
                if (parameter is null)
                {
                    return;
                }

                await appCommand.ExecuteAsync(parameter, cancellationToken);
            });
    }

    public static IAsyncRelayCommand<T> ToAsyncRelayCommand<T>(this IAsyncConditionalAppCommand<T> appCommand) where T : notnull
    {
        return new AsyncRelayCommand<T>(
            cancelableExecute: async (parameter, cancellationToken) =>
            {
                if (parameter is null)
                {
                    return;
                }

                await appCommand.ExecuteAsync(parameter, cancellationToken);
            },
            canExecute: parameter =>
            {
                return parameter is not null
                    && appCommand.CanExecute(parameter);
            });
    }
}
