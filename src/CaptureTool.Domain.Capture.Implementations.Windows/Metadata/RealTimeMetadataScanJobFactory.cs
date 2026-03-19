using CaptureTool.Application.Interfaces;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;
using CaptureTool.Infrastructure.Interfaces.Logging;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata;

/// <summary>
/// Factory for creating real-time metadata scan jobs.
/// </summary>
public sealed class RealTimeMetadataScanJobFactory : IRealTimeMetadataScanJobFactory
{
    private readonly ILogService _logService;
    private readonly IMetadataGroomingPipeline? _groomingPipeline;

    public RealTimeMetadataScanJobFactory(
        ILogService logService,
        IMetadataGroomingPipeline? groomingPipeline = null)
    {
        _logService = logService;
        _groomingPipeline = groomingPipeline;
    }

    public IRealTimeMetadataScanJob CreateJob(
        Guid jobId,
        string filePath,
        IMetadataScannerRegistry registry,
        IMetadataGroomingPipeline? groomingPipeline = null)
    {
        // Caller-supplied pipeline takes precedence over the injected one
        var pipeline = groomingPipeline ?? _groomingPipeline;
        return new RealTimeMetadataScanJob(jobId, filePath, registry, _logService, pipeline);
    }
}
