namespace CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

public interface IMetadataProcessorRegistry
{
    void Register(IMetadataProcessor processor);

    bool Unregister(string processorId);

    IReadOnlyList<IMetadataProcessor> GetAll();
}
