namespace CaptureTool.Application.Abstractions.Messaging.Queries;

public interface IAppQuery<TResult>
{
    /// <summary>
    /// Executes the query for a result.
    /// </summary>
    TResult Execute();
}
