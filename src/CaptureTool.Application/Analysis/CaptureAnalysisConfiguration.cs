using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Analysis;

/// <summary>The single composition point for step order, model preference, and attempt budgets.</summary>
public sealed class CaptureAnalysisConfiguration
{
    public IReadOnlyList<MediaAnalysisPlan> Plans { get; }

    public CaptureAnalysisConfiguration(IEnumerable<MediaAnalysisPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        MediaAnalysisPlan[] copy = plans.ToArray();
        if (copy.Length == 0 || copy.Any(plan => plan == null) || copy.Select(plan => plan.MediaKind).Distinct().Count() != copy.Length)
            throw new ArgumentException("Specify one plan per supported media kind.", nameof(plans));
        Plans = Array.AsReadOnly(copy);
    }

    /// <summary>Call when composing the worker, after the configured adapters have been registered.</summary>
    public void ValidateAnalyzers(IEnumerable<MediaAnalyzerDescriptor> analyzers)
    {
        ArgumentNullException.ThrowIfNull(analyzers);
        MediaAnalyzerDescriptor[] copy = analyzers.ToArray();
        if (copy.Any(analyzer => analyzer == null) || copy.Select(analyzer => analyzer.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("Analyzer identities must be unique.", nameof(analyzers));
        var byId = copy.ToDictionary(analyzer => analyzer.Id, StringComparer.Ordinal);
        foreach (MediaAnalysisPlan plan in Plans)
        foreach (AnalysisStep step in plan.Steps)
        foreach (string id in step.Candidates)
        {
            if (!byId.TryGetValue(id, out MediaAnalyzerDescriptor? analyzer) ||
                analyzer.Capability != step.Capability || !analyzer.SupportedMedia.Contains(plan.MediaKind))
                throw new ArgumentException($"Analyzer '{id}' is missing or incompatible with the configured step.", nameof(analyzers));
        }
    }

    public static CaptureAnalysisConfiguration CreateDefault() => new([
        new(AnalysisMediaKind.Image, "image-v1", [
            Step(AnalysisCapability.TextRecognition, ["windows-ai-ocr-document", "windows-ocr-document"], 2),
            Step(AnalysisCapability.Description, ["windows-image-description"], 2),
        ]),
        new(AnalysisMediaKind.Audio, "audio-v1", [
            Step(AnalysisCapability.Transcription, ["foundry-local-nemotron-multilingual-speech-transcript", "foundry-local-speech-transcript"], 30),
        ]),
        new(AnalysisMediaKind.Video, "video-v1", [
            Step(AnalysisCapability.TextRecognition, ["windows-ai-video-frame-ocr", "windows-video-frame-ocr"], 15),
            Step(AnalysisCapability.Transcription, ["foundry-local-nemotron-multilingual-speech-transcript", "foundry-local-speech-transcript"], 30),
            Step(AnalysisCapability.Description, ["windows-video-frame-description"], 15),
        ]),
    ]);

    private static AnalysisStep Step(AnalysisCapability capability, string[] candidates, int executionMinutes) =>
        new(capability, candidates, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(executionMinutes));
}
