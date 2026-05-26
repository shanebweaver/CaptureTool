namespace CaptureTool.Infrastructure.Abstractions.Commands;

/// <summary>
/// Represents a command that can be executed with a parameter of type <typeparamref name="TParameter"/>.
/// This is a platform-agnostic command interface that can be converted to platform-specific command types.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public interface IAppCommand<in TParameter>
{
    /// <summary>
    /// Executes the command with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    void Execute(TParameter parameter);

    /// <summary>
    /// Determines whether the command can execute with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute(TParameter parameter);

    /// <summary>
    /// Occurs when the ability to execute the command has changed.
    /// </summary>
    event EventHandler? CanExecuteChanged;
}
