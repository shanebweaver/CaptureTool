namespace CaptureTool.Infrastructure.Abstractions.Queries;

public interface IAsyncAppQuery<TResult>
{
    /// <summary>
    /// Executes the command asynchronously..
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    Task<TResult> ExecuteAsync(CancellationToken cancellationToken);

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