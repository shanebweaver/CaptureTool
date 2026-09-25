using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Domain.Analysis;

public sealed record AnalysisResult
{
    public AnalysisPayload Payload { get; }
    public AnalyzerProvenance Producer { get; }
    public DateTimeOffset GeneratedAt { get; }
    public string PlanVersion { get; }
    public Guid? ProducingRunId { get; }
    public Guid ResultId { get; }
    public AnalysisDerivation? Derivation { get; }

    public AnalysisResult(AnalysisPayload payload, AnalyzerProvenance producer, DateTimeOffset generatedAt, string planVersion,
        Guid? producingRunId = null, Guid? resultId = null, AnalysisDerivation? derivation = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(producer);
        Payload = payload;
        Producer = producer;
        GeneratedAt = generatedAt;
        PlanVersion = AnalysisGuard.Identifier(planVersion, nameof(planVersion));
        if (producingRunId == Guid.Empty) throw new ArgumentException("Run identity cannot be empty.", nameof(producingRunId));
        ProducingRunId = producingRunId;
        if (resultId == Guid.Empty) throw new ArgumentException("Result identity cannot be empty.", nameof(resultId));
        if ((payload is DerivedAnalysisPayload) != (derivation != null))
            throw new ArgumentException("Only derived payloads require an input snapshot.", nameof(derivation));
        ResultId = resultId ?? Guid.NewGuid();
        Derivation = derivation;
    }
}
