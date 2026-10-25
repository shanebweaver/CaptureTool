namespace CaptureTool.Domain.Analysis.Payloads;

/// <summary>Metadata produced solely from declared first-level metadata inputs.</summary>
public abstract class DerivedAnalysisPayload : AnalysisPayload
{
    /// <summary>Reject unresolvable or unsupported evidence before publication or consumption.</summary>
    public abstract void ValidateEvidence(IReadOnlyList<AnalysisResult> inputs);
}
