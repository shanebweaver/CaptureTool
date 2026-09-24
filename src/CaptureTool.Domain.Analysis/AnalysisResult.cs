using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Domain.Analysis;

public sealed record AnalysisResult
{
    public AnalysisPayload Payload { get; }
    public AnalyzerProvenance Producer { get; }
    public DateTimeOffset GeneratedAt { get; }
    public string PlanVersion { get; }
    public Guid? ProducingRunId { get; }

    public AnalysisResult(AnalysisPayload payload, AnalyzerProvenance producer, DateTimeOffset generatedAt, string planVersion,
        Guid? producingRunId = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(producer);
        Payload = payload;
        Producer = producer;
        GeneratedAt = generatedAt;
        PlanVersion = AnalysisGuard.Identifier(planVersion, nameof(planVersion));
        if (producingRunId == Guid.Empty) throw new ArgumentException("Run identity cannot be empty.", nameof(producingRunId));
        ProducingRunId = producingRunId;
    }
}
