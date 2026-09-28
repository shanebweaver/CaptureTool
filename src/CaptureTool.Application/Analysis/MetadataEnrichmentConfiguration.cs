using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Analysis;

public sealed record StructuredFactsOptions
{
    public MetadataProcessingLimits Limits { get; }
    public int MaxFacts { get; }
    public int MaxEvidencePerFact { get; }

    public StructuredFactsOptions(MetadataProcessingLimits limits, int maxFacts, int maxEvidencePerFact)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (maxFacts is < 1 or > StructuredFactsMetadata.MaximumFactCount) throw new ArgumentOutOfRangeException(nameof(maxFacts));
        if (maxEvidencePerFact is < 1 or > StructuredFact.MaximumEvidenceCount) throw new ArgumentOutOfRangeException(nameof(maxEvidencePerFact));
        Limits = limits;
        MaxFacts = maxFacts;
        MaxEvidencePerFact = maxEvidencePerFact;
    }
}

/// <summary>Metadata step order, processor contracts, candidate preferences, and budgets.</summary>
public static class MetadataEnrichmentConfiguration
{
    public static IEnumerable<MetadataModelRegistration> SemanticModels =>
        TextModels.SelectMany(model => new[] { AnalysisCapability.CaptureSynopsis, AnalysisCapability.CaptureName, AnalysisCapability.CaptureClassification, AnalysisCapability.ImageAltText }
            .Select(capability => new MetadataModelRegistration(CreateSemantic(model.Id, capability), model.Backend, model.Alias)));
    public static IEnumerable<AnalysisStep> Steps
    {
        get
        {
            MetadataProcessorDescriptor facts = CreateStructuredFacts(StructuredFacts.Limits);
            yield return new(facts.Capability, [facts.Id], TimeSpan.FromSeconds(10), facts.Limits.ExecutionTimeout);
            foreach (AnalysisCapability capability in new[] { AnalysisCapability.CaptureSynopsis, AnalysisCapability.CaptureName, AnalysisCapability.CaptureClassification })
            {
                MetadataProcessorDescriptor[] candidates = TextModels.Select(model => CreateSemantic(model.Id, capability)).ToArray();
                yield return new(capability, candidates.Select(candidate => candidate.Id), TimeSpan.FromMinutes(10), candidates[0].Limits.ExecutionTimeout);
            }
        }
    }
    public static AnalysisStep AltTextStep => new(AnalysisCapability.ImageAltText,
        TextModels.Select(model => CreateSemantic(model.Id, AnalysisCapability.ImageAltText).Id),
        TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(2));

    public static StructuredFactsOptions StructuredFacts { get; } = new(new(2048, 131072, 32768, TimeSpan.FromSeconds(2)), 256, 16);
    public static IReadOnlyList<LocalTextModelDefinition> TextModels { get; } = Array.AsReadOnly(new[]
    {
        new LocalTextModelDefinition("windows-language-model", null, MetadataModelBackend.WindowsLanguageModel),
        new LocalTextModelDefinition("foundry-phi4-mini", "phi-4-mini"),
    });

    public static MetadataProcessorDescriptor CreateSemantic(string modelId, AnalysisCapability capability)
    {
        string suffix = capability == AnalysisCapability.CaptureSynopsis ? "synopsis" :
            capability == AnalysisCapability.CaptureName ? "name" :
            capability == AnalysisCapability.CaptureClassification ? "classification" :
            capability == AnalysisCapability.ImageAltText ? "alt-text" :
            throw new ArgumentException("Unsupported semantic capability.", nameof(capability));
        return new(modelId + "-" + suffix, capability == AnalysisCapability.CaptureName ? "1" :
            capability == AnalysisCapability.CaptureSynopsis ? "4" : "3", capability,
            capability == AnalysisCapability.ImageAltText
                ? [AnalysisCapability.Description, AnalysisCapability.TextRecognition]
                : capability == AnalysisCapability.CaptureSynopsis || capability == AnalysisCapability.CaptureName
                ? [AnalysisCapability.Description, AnalysisCapability.TextRecognition, AnalysisCapability.Transcription]
                : [AnalysisCapability.Description, AnalysisCapability.TextRecognition, AnalysisCapability.Transcription, AnalysisCapability.QrCodeDetection],
            new(64, 4096, 1024, TimeSpan.FromMinutes(2)));
    }

    public static MetadataProcessorDescriptor CreateStructuredFacts(MetadataProcessingLimits limits) =>
        new("local-structured-facts", "1", AnalysisCapability.StructuredFacts,
            [AnalysisCapability.TextRecognition, AnalysisCapability.QrCodeDetection, AnalysisCapability.Transcription],
            limits);
}

public sealed record LocalTextModelDefinition(string Id, string? Alias, MetadataModelBackend Backend = MetadataModelBackend.FoundryLocal);
