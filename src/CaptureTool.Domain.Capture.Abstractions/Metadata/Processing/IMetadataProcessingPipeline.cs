namespace CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

public interface IMetadataProcessingPipeline
{
    Task<RefinedMetadataFile?> ProcessAsync(
        MetadataFile rawMetadata,
        CancellationToken cancellationToken = default);

    Task<string?> ProcessAndSaveAsync(
        MetadataFile rawMetadata,
        string rawMetadataFilePath,
        CancellationToken cancellationToken = default);
}
