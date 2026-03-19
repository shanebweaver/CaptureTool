using CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Grooming;

/// <summary>
/// Thread-safe registry for metadata groomers.
/// </summary>
public sealed class MetadataGroomerRegistry : IMetadataGroomerRegistry
{
    // Use a list to preserve registration order for deterministic pipeline execution.
    private readonly List<IMetadataGroomer> _groomers = [];
    private readonly Dictionary<string, IMetadataGroomer> _groomerById = new();
    private readonly object _lock = new();

    public void Register(IMetadataGroomer groomer)
    {
        ArgumentNullException.ThrowIfNull(groomer);

        lock (_lock)
        {
            if (_groomerById.ContainsKey(groomer.GroomerId))
            {
                throw new InvalidOperationException(
                    $"A groomer with ID '{groomer.GroomerId}' is already registered.");
            }

            _groomers.Add(groomer);
            _groomerById[groomer.GroomerId] = groomer;
        }
    }

    public bool Unregister(string groomerId)
    {
        ArgumentNullException.ThrowIfNull(groomerId);

        lock (_lock)
        {
            if (!_groomerById.Remove(groomerId, out var groomer))
            {
                return false;
            }

            _groomers.Remove(groomer);
            return true;
        }
    }

    public IReadOnlyList<IMetadataGroomer> GetAll()
    {
        lock (_lock)
        {
            return _groomers.ToList();
        }
    }
}
