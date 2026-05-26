namespace CaptureTool.Infrastructure.Abstractions.Queries;

public interface IAppQuery<TResult>
{
    /// <summary>
    /// Executes the query for a result.
    /// </summary>
    TResult Execute();

    /// <summary>
    /// Determines whether the query can execute.
    /// </summary>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute();

    /// <summary>
    /// Occurs when the ability to execute the query has changed.
    /// </summary>
    event EventHandler? CanExecuteChanged;
}
