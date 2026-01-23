namespace CaptureTool.Infrastructure.Interfaces.Commands;

/// <summary>
/// Represents a command that can be executed without parameters.
/// This is a platform-agnostic command interface that can be converted to platform-specific command types.
/// </summary>
public interface IAppCommand
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Determines whether the command can execute.
    /// </summary>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute();

    /// <summary>
    /// Occurs when the ability to execute the command has changed.
    /// </summary>
    event EventHandler? CanExecuteChanged;
}
