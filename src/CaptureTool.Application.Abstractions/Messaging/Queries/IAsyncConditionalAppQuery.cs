namespace CaptureTool.Application.Abstractions.Messaging.Queries;

public interface IAsyncConditionalAppQuery<TResult> : IAsyncAppQuery<TResult>
{
    /// <summary>
    /// Determines whether the command can execute with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute();
}