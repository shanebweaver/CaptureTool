using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Queries;

public interface ICaptureAnalysisQueryService
{
    ValueTask<CaptureAnalysisRecord?> GetAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default);

    ValueTask<CapabilityAnalysis?> GetCapabilityAsync(
        CaptureId captureId,
        CapabilityDefinition capability,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<CaptureAnalysisRecord> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
