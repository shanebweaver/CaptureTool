using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

/// <summary>The single composition point for step order, model preference, and attempt budgets.</summary>
public sealed class CaptureAnalysisConfiguration
{
    public IReadOnlyList<MediaAnalysisPlan> Plans { get; }
    public int MaximumBatchCaptures { get; }

    /// <summary>Selects requested work. Sources keep their configured order; independent LLM outputs follow request order.</summary>
    public MediaAnalysisPlan SelectPlan(AnalysisMediaKind kind, IReadOnlyList<AnalysisCapability>? capabilities = null)
    {
        MediaAnalysisPlan configured = Plans.Single(plan => plan.MediaKind == kind);
        if (capabilities == null) return configured;
        if (capabilities.Count == 0 || capabilities.Distinct().Count() != capabilities.Count ||
            capabilities.Any(capability => !configured.Steps.Any(step => step.Capability == capability)))
            throw new ArgumentException("Request supported, distinct analysis capabilities.", nameof(capabilities));
        var selected = configured.Steps.Where(step => capabilities.Contains(step.Capability)).ToArray();
        return new(kind, configured.Version,
            selected.Where(step => !MetadataEnrichmentConfiguration.LanguageModelCapabilities.Contains(step.Capability))
                .Concat(capabilities.Where(MetadataEnrichmentConfiguration.LanguageModelCapabilities.Contains)
                    .Select(capability => selected.Single(step => step.Capability == capability))));
    }

    public CaptureAnalysisConfiguration(IEnumerable<MediaAnalysisPlan> plans, int maximumBatchCaptures = 16)
    {
        ArgumentNullException.ThrowIfNull(plans);
        if (maximumBatchCaptures is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(maximumBatchCaptures));
        MaximumBatchCaptures = maximumBatchCaptures;
        MediaAnalysisPlan[] copy = plans.ToArray();
        if (copy.Length == 0 || copy.Any(plan => plan == null) || copy.Select(plan => plan.MediaKind).Distinct().Count() != copy.Length)
            throw new ArgumentException("Specify one plan per supported media kind.", nameof(plans));
        Plans = Array.AsReadOnly(copy);
    }

    /// <summary>Call when composing the worker, after the configured adapters have been registered.</summary>
    public void ValidateAnalyzers(IEnumerable<MediaAnalyzerDescriptor> analyzers,
        IEnumerable<MetadataProcessorDescriptor>? processors = null)
    {
        ArgumentNullException.ThrowIfNull(analyzers);
        MediaAnalyzerDescriptor[] copy = analyzers.ToArray();
        if (copy.Any(analyzer => analyzer == null) || copy.Select(analyzer => analyzer.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Analyzer identities must be unique.", nameof(analyzers));
        var byId = copy.ToDictionary(analyzer => analyzer.Id, StringComparer.Ordinal);
        MetadataProcessorDescriptor[] metadata = processors?.ToArray() ?? [];
        if (metadata.Any(processor => processor == null) || metadata.Select(processor => processor.Id).Distinct(StringComparer.Ordinal).Count() != metadata.Length ||
            metadata.Any(processor => byId.ContainsKey(processor.Id)))
            throw new ArgumentException("Provider identities must be unique.", nameof(processors));
        var metadataById = metadata.ToDictionary(processor => processor.Id, StringComparer.Ordinal);
        foreach (MediaAnalysisPlan plan in Plans)
        {
            bool metadataStarted = false;
            foreach (AnalysisStep step in plan.Steps)
            {
                MetadataProcessorDescriptor? contract = metadataById.GetValueOrDefault(step.Candidates[0]);
                if (contract != null) metadataStarted = true;
                else if (metadataStarted) throw new ArgumentException("All media steps must precede metadata processing.", nameof(processors));
                foreach (string id in step.Candidates)
                {
                    if (contract != null)
                    {
                        if (!metadataById.TryGetValue(id, out MetadataProcessorDescriptor? processor) || processor.Capability != step.Capability ||
                            processor.Version != contract.Version || !processor.Inputs.SequenceEqual(contract.Inputs) || processor.Limits != contract.Limits ||
                            step.ExecutionTimeout > processor.Limits.ExecutionTimeout)
                            throw new ArgumentException($"Processor '{id}' is missing or incompatible with the configured contract.", nameof(processors));
                        continue;
                    }
                    if (!byId.TryGetValue(id, out MediaAnalyzerDescriptor? analyzer) ||
                        analyzer.Capability != step.Capability || !analyzer.SupportedMedia.Contains(plan.MediaKind))
                        throw new ArgumentException($"Analyzer '{id}' is missing or incompatible with the configured step.", nameof(analyzers));
                }
            }
        }
    }

    /// <summary>One LLM action prepares the capture's other supported LLM outputs while the model is resident.</summary>
    public static IReadOnlyList<AnalysisCapability> ForAction(AnalysisMediaKind kind, AnalysisCapability capability)
    {
        var outputs = MetadataEnrichmentConfiguration.LanguageModelCapabilities
            .Where(output => kind == AnalysisMediaKind.Image || output != AnalysisCapability.ImageAltText).ToArray();
        if (!outputs.Contains(capability)) return [capability];
        // Publish the requested output first. Remaining outputs reuse the same source evidence and model.
        return [.. kind == AnalysisMediaKind.Image ? new[] { AnalysisCapability.TextRecognition, AnalysisCapability.Description } : [],
            capability, .. outputs.Where(output => output != capability)];
    }

    public static CaptureAnalysisConfiguration CreateDefault() => new([
        new(AnalysisMediaKind.Image, "image-v9", [
            Step(AnalysisCapability.FileDetails, ["windows-file-details"], 1),
            Step(AnalysisCapability.QrCodeDetection, ["zxing-image-qr"], 2),
            Step(AnalysisCapability.TextRecognition, ["windows-ai-ocr-document", "windows-ocr-document"], 2),
            Step(AnalysisCapability.Description, ["windows-image-description", "foundry-local-image-description"], 2),
            .. MetadataEnrichmentConfiguration.Steps,
            MetadataEnrichmentConfiguration.AltTextStep,
        ]),
        new(AnalysisMediaKind.Audio, "audio-v6", [
            Step(AnalysisCapability.FileDetails, ["windows-file-details"], 1),
            Step(AnalysisCapability.Transcription, ["foundry-local-nemotron-multilingual-speech-transcript", "foundry-local-speech-transcript"], 30),
            .. MetadataEnrichmentConfiguration.Steps,
        ]),
        new(AnalysisMediaKind.Video, "video-v8", [
            Step(AnalysisCapability.FileDetails, ["windows-file-details"], 1),
            Step(AnalysisCapability.QrCodeDetection, ["zxing-video-frame-qr"], 15),
            Step(AnalysisCapability.TextRecognition, ["windows-ai-video-frame-ocr", "windows-video-frame-ocr"], 15),
            Step(AnalysisCapability.Transcription, ["foundry-local-nemotron-multilingual-speech-transcript", "foundry-local-speech-transcript"], 30),
            Step(AnalysisCapability.Description, ["windows-video-frame-description", "foundry-local-image-description"], 15),
            .. MetadataEnrichmentConfiguration.Steps,
        ]),
    ]);

    private static AnalysisStep Step(AnalysisCapability capability, string[] candidates, int executionMinutes) =>
        new(capability, candidates, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(executionMinutes));
}
