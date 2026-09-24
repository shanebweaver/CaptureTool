namespace CaptureTool.Domain.Analysis;

/// <summary>Canonical successful results for one capture revision, independent of job scheduling.</summary>
public sealed class CaptureAnalysisRecord
{
    public CaptureId CaptureId { get; }
    public AnalysisMediaKind MediaKind { get; }
    public SourceRevision SourceRevision { get; }
    public string PlanVersion { get; }
    public Guid RunId { get; }
    public IReadOnlyList<AnalysisResult> Results { get; }

    public CaptureAnalysisRecord(CaptureId captureId, AnalysisMediaKind mediaKind, SourceRevision sourceRevision,
        string planVersion, Guid runId, IEnumerable<AnalysisResult> results)
    {
        if (captureId.IsEmpty) throw new ArgumentException("Capture identity is required.", nameof(captureId));
        if (!Enum.IsDefined(mediaKind)) throw new ArgumentOutOfRangeException(nameof(mediaKind));
        if (runId == Guid.Empty) throw new ArgumentException("Run identity is required.", nameof(runId));
        ArgumentNullException.ThrowIfNull(sourceRevision);
        CaptureId = captureId;
        MediaKind = mediaKind;
        SourceRevision = sourceRevision;
        PlanVersion = AnalysisGuard.Identifier(planVersion, nameof(planVersion));
        RunId = runId;
        Results = AnalysisGuard.Freeze(results);
        if (Results.Select(result => result.Payload.Capability).Distinct().Count() != Results.Count)
            throw new ArgumentException("Only one canonical result per capability is allowed.", nameof(results));
        if (Results.Any(result => !result.Payload.Supports(mediaKind)))
            throw new ArgumentException("Result does not support this media kind.", nameof(results));
    }

    public CaptureAnalysisRecord StartRun(SourceRevision revision, string planVersion, Guid runId) =>
        new(CaptureId, MediaKind, revision, planVersion, runId, revision == SourceRevision ? Results : []);

    public CaptureAnalysisRecord WithResult(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.PlanVersion != PlanVersion) throw new ArgumentException("Result belongs to a different plan version.", nameof(result));
        return new(CaptureId, MediaKind, SourceRevision, PlanVersion, RunId,
            Results.Where(existing => existing.Payload.Capability != result.Payload.Capability).Append(result));
    }
}
