using CaptureTool.Domain.Capture.Abstractions.Metadata;

namespace CaptureTool.Application.Abstractions.Metadata;

public interface IRealTimeMetadataScanJobFactory
{
    IRealTimeMetadataScanJob CreateJob(Guid jobId, string filePath, IMetadataScannerRegistry registry);
}
