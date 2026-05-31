using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

namespace CaptureTool.Application.Abstractions.Metadata;

public interface IRealTimeMetadataScanJobFactory
{
    IRealTimeMetadataScanJob CreateJob(
        Guid jobId,
        string filePath,
        IMetadataScannerRegistry registry,
        IMetadataProcessingPipeline? processingPipeline = null);
}
