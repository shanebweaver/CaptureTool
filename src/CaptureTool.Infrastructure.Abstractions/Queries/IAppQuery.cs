namespace CaptureTool.Infrastructure.Abstractions.Queries;

public interface IAppQuery<TResult>
{
    /// <summary>
    /// Executes the query for a result.
    /// </summary>
    TResult Execute();
}
