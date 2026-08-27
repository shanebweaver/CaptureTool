using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Checkpoints;

public readonly record struct CaptureAnalysisCheckpointKey
{
    public CaptureAnalysisCheckpointKey(
        CaptureId captureId,
        SourceRevision sourceRevision,
        CapabilityDefinition capability,
        AnalyzerRevision analyzerRevision)
    {
        if (captureId.IsEmpty || sourceRevision.IsEmpty || capability.Id.IsEmpty ||
            analyzerRevision.IsEmpty)
        {
            throw new ArgumentException(
                "A checkpoint key requires a capture, source, capability, and analyzer revision.");
        }

        CaptureId = captureId;
        SourceRevision = sourceRevision;
        Capability = capability;
        AnalyzerRevision = analyzerRevision;
    }

    public CaptureId CaptureId { get; }

    public SourceRevision SourceRevision { get; }

    public CapabilityDefinition Capability { get; }

    public AnalyzerRevision AnalyzerRevision { get; }
}

public interface ICaptureAnalysisCheckpointStore
{
    ICaptureAnalyzerCheckpoint Open(CaptureAnalysisCheckpointKey key);

    ValueTask DeleteCaptureAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);

    ValueTask<int> PruneAsync(
        DateTimeOffset olderThanUtc,
        CancellationToken cancellationToken = default);
}
