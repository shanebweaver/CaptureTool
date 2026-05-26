namespace CaptureTool.Infrastructure.Abstractions.Queries;

public interface IConditionalAppQuery<in TParameter, TResult> : IAppQueryT<TParameter, TResult> where TParameter : notnull
{
    /// <summary>
    /// Determines whether the query can execute with the specified parameter.
    /// </summary>
    /// <param name="parameter">The query parameter.</param>
    /// <returns>True if the query can execute; otherwise, false.</returns>
    bool CanExecute(TParameter parameter);
}
