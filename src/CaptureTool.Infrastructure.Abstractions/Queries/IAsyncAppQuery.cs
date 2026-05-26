namespace CaptureTool.Infrastructure.Abstractions.Queries;

public interface IAsyncAppQuery<TResult>
{
    /// <summary>
    /// Executes the command asynchronously..
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    Task<TResult> ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    bool IsExecuting { get; }
}
