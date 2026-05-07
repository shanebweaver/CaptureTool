using CaptureTool.Domain.Capture.Interfaces.Metadata.Processing;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Processing;

/// <summary>
/// Thread-safe registry for metadata processors.
/// </summary>
public sealed class MetadataProcessorRegistry : IMetadataProcessorRegistry
{
    // Use a list to preserve registration order for deterministic pipeline execution.
    private readonly List<IMetadataProcessor> _processors = [];
    private readonly Dictionary<string, IMetadataProcessor> _processorById = new();
    private readonly object _lock = new();

    public void Register(IMetadataProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);

        lock (_lock)
        {
            if (_processorById.ContainsKey(processor.ProcessorId))
            {
                throw new InvalidOperationException(
                    $"A processor with ID '{processor.ProcessorId}' is already registered.");
            }

            _processors.Add(processor);
            _processorById[processor.ProcessorId] = processor;
        }
    }

    public bool Unregister(string processorId)
    {
        ArgumentNullException.ThrowIfNull(processorId);

        lock (_lock)
        {
            if (!_processorById.Remove(processorId, out var processor))
            {
                return false;
            }

            _processors.Remove(processor);
            return true;
        }
    }

    public IReadOnlyList<IMetadataProcessor> GetAll()
    {
        lock (_lock)
        {
            return _processors.ToList();
        }
    }
}
