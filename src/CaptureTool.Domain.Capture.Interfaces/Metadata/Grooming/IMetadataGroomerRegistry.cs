namespace CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

/// <summary>
/// Registry for managing metadata groomers.
/// Groomers are registered here to form the second processing layer.
/// </summary>
public interface IMetadataGroomerRegistry
{
    /// <summary>
    /// Registers a metadata groomer.
    /// </summary>
    /// <param name="groomer">The groomer to register.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a groomer with the same ID is already registered.
    /// </exception>
    void Register(IMetadataGroomer groomer);

    /// <summary>
    /// Unregisters a groomer by its ID.
    /// </summary>
    /// <param name="groomerId">The ID of the groomer to unregister.</param>
    /// <returns>True if the groomer was found and removed; false otherwise.</returns>
    bool Unregister(string groomerId);

    /// <summary>
    /// Gets all registered groomers in the order they were registered.
    /// </summary>
    IReadOnlyList<IMetadataGroomer> GetAll();
}
