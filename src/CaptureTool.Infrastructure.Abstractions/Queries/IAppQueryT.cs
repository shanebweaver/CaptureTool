namespace CaptureTool.Infrastructure.Abstractions.Queries;

/// <summary>
/// Represents a query that can be executed with a parameter of type <typeparamref name="T"/>.
/// This is a platform-agnostic query interface that can be converted to platform-specific query types.
/// </summary>
/// <typeparam name="T">The type of the query parameter.</typeparam>
public interface IAppQueryT<in TParameter, TResult>
{
    /// <summary>
    /// Executes the query with the specified parameter.
    /// </summary>
    /// <param name="parameter">The query parameter.</param>
    TResult Execute(TParameter parameter);

    /// <summary>
    /// Determines whether the query can execute with the specified parameter.
    /// </summary>
    /// <param name="parameter">The query parameter.</param>
    /// <returns>True if the query can execute; otherwise, false.</returns>
    bool CanExecute(TParameter parameter);

    /// <summary>
    /// Occurs when the ability to execute the query has changed.
    /// </summary>
    event EventHandler? CanExecuteChanged;
}
