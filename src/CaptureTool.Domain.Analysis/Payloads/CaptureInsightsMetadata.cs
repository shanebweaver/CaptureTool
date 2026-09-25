namespace CaptureTool.Domain.Analysis.Payloads;

/// <summary>A model inference with source evidence. It is never an observed structured fact.</summary>
public sealed class SuggestedText
{
    public string Text { get; }
    public IReadOnlyList<AnalysisEvidence> Evidence { get; }

    public SuggestedText(string text, IEnumerable<AnalysisEvidence> evidence)
    {
        Text = InsightValidation.Text(text, 400);
        Evidence = InsightValidation.Evidence(evidence, required: true);
    }
}

/// <summary>Suggested content is separate from file names and user-authored titles.</summary>
public sealed class CaptureSynopsisMetadata : DerivedAnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.CaptureSynopsis;
    public SuggestedText? Title { get; }
    public IReadOnlyList<SuggestedText> Summary { get; }
    public MetadataProcessingCoverage Coverage { get; }

    public CaptureSynopsisMetadata(SuggestedText? title, IEnumerable<SuggestedText> summary, MetadataProcessingCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (title?.Text.Length > 160) throw new ArgumentException("Suggested title is too long.", nameof(title));
        Summary = AnalysisGuard.Freeze(summary);
        if (Summary.Count > 3 || Summary.Select(item => item.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Summary.Count)
            throw new ArgumentException("Specify at most three distinct summary statements.", nameof(summary));
        Title = title;
        Coverage = coverage;
    }

    public override bool Supports(AnalysisMediaKind mediaKind) => Enum.IsDefined(mediaKind);
    public override void ValidateEvidence(IReadOnlyList<AnalysisResult> inputs) => InsightValidation.Validate(inputs, Coverage,
        (Title == null ? Summary : Summary.Prepend(Title)).SelectMany(item => item.Evidence));
}

public enum CaptureCategory
{
    Document = 0,
    Conversation = 1,
    Code = 2,
    Error = 3,
    WebContent = 4,
    Media = 5,
    Other = 6,
}

public sealed class CaptureClassificationMetadata : DerivedAnalysisPayload
{
    public override AnalysisCapability Capability => AnalysisCapability.CaptureClassification;
    public const string VocabularyVersion = "1";
    public CaptureCategory? Category { get; }
    public IReadOnlyList<AnalysisEvidence> CategoryEvidence { get; }
    public IReadOnlyList<SuggestedText> Topics { get; }
    public MetadataProcessingCoverage Coverage { get; }

    public CaptureClassificationMetadata(CaptureCategory? category, IEnumerable<AnalysisEvidence> categoryEvidence,
        IEnumerable<SuggestedText> topics, MetadataProcessingCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (category is { } value && !Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(category));
        CategoryEvidence = InsightValidation.Evidence(categoryEvidence, required: category != null);
        if (category == null && CategoryEvidence.Count != 0) throw new ArgumentException("An abstention has no category evidence.", nameof(categoryEvidence));
        Topics = AnalysisGuard.Freeze(topics);
        if (Topics.Count > 5 || Topics.Any(topic => topic.Text.Length > 48 || topic.Text != topic.Text.ToLowerInvariant()) ||
            Topics.Select(topic => topic.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Topics.Count)
            throw new ArgumentException("Specify at most five distinct lowercase topics of at most 48 characters.", nameof(topics));
        Category = category;
        Coverage = coverage;
    }

    public override bool Supports(AnalysisMediaKind mediaKind) => Enum.IsDefined(mediaKind);
    public override void ValidateEvidence(IReadOnlyList<AnalysisResult> inputs) => InsightValidation.Validate(inputs, Coverage,
        CategoryEvidence.Concat(Topics.SelectMany(topic => topic.Evidence)));
}

internal static class InsightValidation
{
    internal static string Text(string text, int maximum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Length > maximum || text != text.Trim() || text.Any(char.IsControl))
            throw new ArgumentException("Insight text must be bounded plain text without control characters.", nameof(text));
        for (int index = 0; index < text.Length; index++)
            if (char.IsSurrogate(text[index]) && (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[++index])))
                throw new ArgumentException("Insight text contains invalid Unicode.", nameof(text));
        return text;
    }

    internal static IReadOnlyList<AnalysisEvidence> Evidence(IEnumerable<AnalysisEvidence> evidence, bool required)
    {
        IReadOnlyList<AnalysisEvidence> copy = AnalysisGuard.Freeze(evidence);
        if (copy.Count > 4 || required && copy.Count == 0 || copy.Distinct().Count() != copy.Count)
            throw new ArgumentException("Specify distinct evidence, with one to four spans for every suggestion.", nameof(evidence));
        return copy;
    }

    internal static void Validate(IReadOnlyList<AnalysisResult> inputs, MetadataProcessingCoverage coverage, IEnumerable<AnalysisEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        AnalysisEvidence[] spans = evidence.ToArray();
        coverage.Validate(inputs, spans);
        foreach (AnalysisEvidence span in spans)
        {
            AnalysisResult? source = inputs.SingleOrDefault(input => input.ResultId == span.ResultId);
            if (source == null) throw new ArgumentException("Insight evidence uses an undeclared source.", nameof(evidence));
            _ = span.ResolveText(source);
        }
    }
}
