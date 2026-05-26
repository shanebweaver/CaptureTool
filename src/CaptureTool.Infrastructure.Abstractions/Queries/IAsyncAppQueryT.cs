namespace CaptureTool.Infrastructure.Abstractions.Queries;

public interface IAsyncAppQueryT<in TParameter, TResult> where TParameter : notnull
{
    /// <summary>
    /// Executes the command asynchronously with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    Task<TResult> ExecuteAsync(TParameter parameter, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    bool IsExecuting { get; }
}
