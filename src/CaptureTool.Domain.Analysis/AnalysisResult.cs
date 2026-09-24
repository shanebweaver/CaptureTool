using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Domain.Analysis;

public sealed record AnalysisResult
{
    public AnalysisPayload Payload { get; }
    public AnalyzerProvenance Producer { get; }
    public DateTimeOffset GeneratedAt { get; }
    public string PlanVersion { get; }

    public AnalysisResult(AnalysisPayload payload, AnalyzerProvenance producer, DateTimeOffset generatedAt, string planVersion)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(producer);
        Payload = payload;
        Producer = producer;
        GeneratedAt = generatedAt;
        PlanVersion = AnalysisGuard.Identifier(planVersion, nameof(planVersion));
    }
}
