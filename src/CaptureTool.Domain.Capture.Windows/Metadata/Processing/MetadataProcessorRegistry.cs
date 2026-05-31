using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

namespace CaptureTool.Domain.Capture.Windows.Metadata.Processing;

public sealed class MetadataProcessorRegistry : IMetadataProcessorRegistry
{
    private readonly List<IMetadataProcessor> _processors = [];
    private readonly Dictionary<string, IMetadataProcessor> _processorById = [];
    private readonly object _lock = new();

    public void Register(IMetadataProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);

        lock (_lock)
        {
            if (_processorById.ContainsKey(processor.ProcessorId))
            {
                throw new InvalidOperationException(
                    $"A metadata processor with ID '{processor.ProcessorId}' is already registered.");
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
            if (!_processorById.Remove(processorId, out IMetadataProcessor? processor))
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
