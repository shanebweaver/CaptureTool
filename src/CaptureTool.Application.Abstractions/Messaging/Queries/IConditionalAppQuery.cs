namespace CaptureTool.Application.Abstractions.Messaging.Queries;

public interface IConditionalAppQuery<TResult> : IAppQuery<TResult>
{
    /// <summary>
    /// Determines whether the query can execute.
    /// </summary>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute();
}
