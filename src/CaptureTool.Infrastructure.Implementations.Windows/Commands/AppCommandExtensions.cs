using CaptureTool.Infrastructure.Interfaces.Commands;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace CaptureTool.Infrastructure.Implementations.Windows.Commands;

/// <summary>
/// Extension methods to convert platform-agnostic app commands to Windows-specific ICommand implementations.
/// </summary>
public static class AppCommandExtensions
{
    /// <summary>
    /// Converts an <see cref="IAppCommand"/> to a <see cref="System.Windows.Input.ICommand"/>.
    /// </summary>
    /// <param name="appCommand">The app command to convert.</param>
    /// <returns>A Windows-specific ICommand implementation.</returns>
    public static ICommand ToICommand(this IAppCommand appCommand)
    {
        ArgumentNullException.ThrowIfNull(appCommand);
        return new RelayCommand(appCommand.Execute, appCommand.CanExecute);
    }

    /// <summary>
    /// Converts an <see cref="IAppCommand{T}"/> to a <see cref="System.Windows.Input.ICommand"/>.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="appCommand">The app command to convert.</param>
    /// <returns>A Windows-specific ICommand implementation.</returns>
    public static ICommand ToICommand<T>(this IAppCommand<T> appCommand)
    {
        ArgumentNullException.ThrowIfNull(appCommand);
        return new RelayCommand<T?>(appCommand.Execute, appCommand.CanExecute);
    }

    /// <summary>
    /// Converts an <see cref="IAsyncAppCommand"/> to a <see cref="System.Windows.Input.ICommand"/>.
    /// </summary>
    /// <param name="asyncAppCommand">The async app command to convert.</param>
    /// <returns>A Windows-specific ICommand implementation.</returns>
    public static ICommand ToICommand(this IAsyncAppCommand asyncAppCommand)
    {
        ArgumentNullException.ThrowIfNull(asyncAppCommand);
        return new AsyncRelayCommand(asyncAppCommand.ExecuteAsync, asyncAppCommand.CanExecute);
    }

    /// <summary>
    /// Converts an <see cref="IAsyncAppCommand{T}"/> to a <see cref="System.Windows.Input.ICommand"/>.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="asyncAppCommand">The async app command to convert.</param>
    /// <returns>A Windows-specific ICommand implementation.</returns>
    public static ICommand ToICommand<T>(this IAsyncAppCommand<T> asyncAppCommand)
    {
        ArgumentNullException.ThrowIfNull(asyncAppCommand);
        return new AsyncRelayCommand<T?>(asyncAppCommand.ExecuteAsync);
    }
}
