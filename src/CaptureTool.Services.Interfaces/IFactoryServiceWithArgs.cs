namespace CaptureTool.Services.Interfaces;

/// <summary>
/// Provides a factory for creating instances of type <typeparamref name="T"/> with arguments.
/// </summary>
/// <typeparam name="T">The type of object to create.</typeparam>
/// <typeparam name="A">The type of arguments required for creation.</typeparam>
public interface IFactoryServiceWithArgs<T, A>
{
    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/> using the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to use for creation.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    T Create(A args);
}
