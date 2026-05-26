namespace CaptureTool.Infrastructure.Abstractions.Queries;

/// <summary>
/// Represents a query that can be executed with a parameter of type <typeparamref name="T"/>.
/// This is a platform-agnostic query interface that can be converted to platform-specific query types.
/// </summary>
/// <typeparam name="T">The type of the query parameter.</typeparam>
public interface IAppQueryT<in TParameter, TResult> where TParameter : notnull
{
    /// <summary>
    /// Executes the query with the specified parameter.
    /// </summary>
    /// <param name="parameter">The query parameter.</param>
    TResult Execute(TParameter parameter);
}