namespace CaptureTool.Domain.Analysis;

/// <summary>The actual producer, including its resolved model version when the provider exposes it.</summary>
public sealed record AnalyzerProvenance
{
    public string AnalyzerId { get; }
    public string ProviderId { get; }
    public string ModelId { get; }
    public string AdapterVersion { get; }
    public string? ModelVersion { get; }

    public AnalyzerProvenance(string analyzerId, string providerId, string modelId,
        string adapterVersion, string? modelVersion = null)
    {
        AnalyzerId = AnalysisGuard.Identifier(analyzerId, nameof(analyzerId));
        ProviderId = AnalysisGuard.Identifier(providerId, nameof(providerId));
        ModelId = AnalysisGuard.Identifier(modelId, nameof(modelId));
        AdapterVersion = AnalysisGuard.Identifier(adapterVersion, nameof(adapterVersion));
        ModelVersion = modelVersion == null ? null : AnalysisGuard.Identifier(modelVersion, nameof(modelVersion));
    }
}
