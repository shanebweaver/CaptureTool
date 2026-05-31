using CaptureTool.Application.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Domain.Capture.Windows.Metadata;

/// <summary>
/// Factory for creating real-time metadata scan jobs.
/// </summary>
public sealed class RealTimeMetadataScanJobFactory : IRealTimeMetadataScanJobFactory
{
    private readonly ILogService _logService;
    private readonly IMetadataProcessingPipeline? _processingPipeline;

    public RealTimeMetadataScanJobFactory(
        ILogService logService,
        IMetadataProcessingPipeline? processingPipeline = null)
    {
        _logService = logService;
        _processingPipeline = processingPipeline;
    }

    public IRealTimeMetadataScanJob CreateJob(
        Guid jobId,
        string filePath,
        IMetadataScannerRegistry registry,
        IMetadataProcessingPipeline? processingPipeline = null)
    {
        return new RealTimeMetadataScanJob(
            jobId,
            filePath,
            registry,
            _logService,
            processingPipeline ?? _processingPipeline);
    }
}
