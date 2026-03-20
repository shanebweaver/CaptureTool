namespace CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;

/// <summary>
/// Registry for managing metadata processors.
/// Processors are registered here to form the second processing layer.
/// </summary>
public interface IMetadataProcessorRegistry
{
    /// <summary>
    /// Registers a metadata processor.
    /// </summary>
    /// <param name="processor">The processor to register.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a processor with the same ID is already registered.
    /// </exception>
    void Register(IMetadataProcessor processor);

    /// <summary>
    /// Unregisters a processor by its ID.
    /// </summary>
    /// <param name="processorId">The ID of the processor to unregister.</param>
    /// <returns>True if the processor was found and removed; false otherwise.</returns>
    bool Unregister(string processorId);

    /// <summary>
    /// Gets all registered processors in the order they were registered.
    /// </summary>
    IReadOnlyList<IMetadataProcessor> GetAll();
}
