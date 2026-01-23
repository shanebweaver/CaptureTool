namespace CaptureTool.Infrastructure.Interfaces.Commands;

/// <summary>
/// Represents an asynchronous command that can be executed without parameters.
/// This is a platform-agnostic command interface that can be converted to platform-specific command types.
/// </summary>
public interface IAsyncAppCommand
{
    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    Task ExecuteAsync();

    /// <summary>
    /// Determines whether the command can execute.
    /// </summary>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute();

    /// <summary>
    /// Occurs when the ability to execute the command has changed.
    /// </summary>
    event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    bool IsExecuting { get; }
}
