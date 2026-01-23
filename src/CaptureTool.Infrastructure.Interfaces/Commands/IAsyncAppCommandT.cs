namespace CaptureTool.Infrastructure.Interfaces.Commands;

/// <summary>
/// Represents an asynchronous command that can be executed with a parameter of type <typeparamref name="T"/>.
/// This is a platform-agnostic command interface that can be converted to platform-specific command types.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public interface IAsyncAppCommand<in T>
{
    /// <summary>
    /// Executes the command asynchronously with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    Task ExecuteAsync(T? parameter);

    /// <summary>
    /// Determines whether the command can execute with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute(T? parameter);

    /// <summary>
    /// Occurs when the ability to execute the command has changed.
    /// </summary>
    event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    bool IsExecuting { get; }
}
