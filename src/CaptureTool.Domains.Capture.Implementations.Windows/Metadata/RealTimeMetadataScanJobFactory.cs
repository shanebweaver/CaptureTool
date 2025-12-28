using CaptureTool.Core.Interfaces;
using CaptureTool.Domains.Capture.Interfaces.Metadata;
using CaptureTool.Services.Interfaces.Logging;

namespace CaptureTool.Domains.Capture.Implementations.Windows.Metadata;

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
