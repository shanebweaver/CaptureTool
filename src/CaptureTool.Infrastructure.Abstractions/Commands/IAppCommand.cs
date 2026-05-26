namespace CaptureTool.Infrastructure.Abstractions.Commands;

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
}
