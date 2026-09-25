using CaptureTool.Domain.Analysis.Payloads;

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
        if (Results.Select(result => result.ResultId).Distinct().Count() != Results.Count)
            throw new ArgumentException("Result identities must be distinct.", nameof(results));
        foreach (AnalysisResult result in Results.Where(result => result.Derivation != null))
        {
            if (!HasCurrentInputs(result)) throw new ArgumentException("Derived metadata refers to stale inputs.", nameof(results));
            ((DerivedAnalysisPayload)result.Payload).ValidateEvidence(Results.Where(input =>
                result.Derivation!.Inputs.Any(reference => reference.ResultId == input.ResultId)).ToArray());
        }
    }

    public CaptureAnalysisRecord StartRun(SourceRevision revision, string planVersion, Guid runId) =>
        new(CaptureId, MediaKind, revision, planVersion, runId, revision == SourceRevision ? Results : []);

    public CaptureAnalysisRecord WithResult(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.PlanVersion != PlanVersion) throw new ArgumentException("Result belongs to a different plan version.", nameof(result));
        if (Results.Any(existing => existing.ResultId == result.ResultId))
            throw new ArgumentException("A replacement requires a new result identity.", nameof(result));
        if (!HasCurrentInputs(result)) throw new ArgumentException("Derived metadata refers to stale inputs.", nameof(result));
        AnalysisResult[] next = Results.Where(existing => existing.Payload.Capability != result.Payload.Capability).Append(result).ToArray();
        return new(CaptureId, MediaKind, SourceRevision, PlanVersion, RunId,
            next.Where(existing => existing.Derivation?.Matches(next) != false));
    }

    public bool HasCurrentInputs(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Derivation?.Matches(Results) != false;
    }
}
