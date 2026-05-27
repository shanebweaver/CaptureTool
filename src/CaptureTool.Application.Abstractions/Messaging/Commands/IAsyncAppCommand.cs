namespace CaptureTool.Application.Abstractions.Messaging.Commands;

/// <summary>
/// Represents an asynchronous command that can be executed without parameters.
/// This is a platform-agnostic command interface that can be converted to platform-specific command types.
/// </summary>
public interface IAsyncAppCommand
{
    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    bool IsExecuting { get; }
}
