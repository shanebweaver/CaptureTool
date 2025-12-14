namespace CaptureTool.Services.Interfaces;

/// <summary>
/// Provides a factory for creating instances of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of object to create.</typeparam>
public interface IFactoryService<T>
{
    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/>.
    /// </summary>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    T Create();
}
