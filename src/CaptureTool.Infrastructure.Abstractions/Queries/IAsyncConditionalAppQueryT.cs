namespace CaptureTool.Infrastructure.Abstractions.Queries;

public interface IAsyncConditionalAppQueryT<in TParameter, TResult> : IAsyncAppQueryT<TParameter, TResult> where TParameter : notnull
{
    /// <summary>
    /// Determines whether the command can execute with the specified parameter.
    /// </summary>
    /// <param name="parameter">The command parameter.</param>
    /// <returns>True if the command can execute; otherwise, false.</returns>
    bool CanExecute(TParameter parameter);
}
