using CaptureTool.Application.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Infrastructure.Abstractions.Logging;

namespace CaptureTool.Domain.Capture.Windows.Metadata;

/// <summary>
/// Factory for creating real-time metadata scan jobs.
/// </summary>
public sealed class RealTimeMetadataScanJobFactory : IRealTimeMetadataScanJobFactory
{
    private readonly ILogService _logService;

    public RealTimeMetadataScanJobFactory(ILogService logService)
    {
        _logService = logService;
    }

    public IRealTimeMetadataScanJob CreateJob(Guid jobId, string filePath, IMetadataScannerRegistry registry)
    {
        return new RealTimeMetadataScanJob(jobId, filePath, registry, _logService);
    }
}
