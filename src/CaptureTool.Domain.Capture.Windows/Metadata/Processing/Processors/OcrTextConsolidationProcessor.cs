using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

namespace CaptureTool.Domain.Capture.Windows.Metadata.Processing.Processors;

public sealed class OcrTextConsolidationProcessor : IMetadataProcessor
{
    private const long MergeWindowTicks = TimeSpan.TicksPerSecond;
    private const double MergeSimilarityThreshold = 0.4;

    public string ProcessorId => "ocr-text-consolidation";

    public string Name => "OCR Text Consolidation Processor";

    public IReadOnlyList<string> SupportedKeys { get; } = ["ocr-text"];

    public Task<IReadOnlyList<InsightEntry>> ProcessAsync(
        IReadOnlyList<MetadataEntry> rawEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawEntries);

        List<OcrTextGroup> groups = [];

        foreach (MetadataEntry entry in rawEntries
            .Where(entry => string.Equals(entry.Key, "ocr-text", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string text = entry.Value?.ToString()?.Trim() ?? string.Empty;
            if (text.Length < 3)
            {
                continue;
            }

            OcrTextGroup? currentGroup = groups.LastOrDefault();
            if (currentGroup is not null && ShouldMerge(currentGroup, entry.Timestamp, text))
            {
                currentGroup.Add(entry, text);
            }
            else
            {
                groups.Add(new OcrTextGroup(entry, text));
            }
        }

        IReadOnlyList<InsightEntry> insights = groups
            .Select(group => group.ToInsight(ProcessorId))
            .OrderBy(insight => insight.Timestamp)
            .ToList();

        return Task.FromResult(insights);
    }

    private static bool ShouldMerge(OcrTextGroup group, long timestamp, string text)
    {
        return timestamp - group.EndTimestamp <= MergeWindowTicks &&
               CalculateWordOverlap(group.Text, text) >= MergeSimilarityThreshold;
    }

    private static double CalculateWordOverlap(string left, string right)
    {
        HashSet<string> leftWords = SplitWords(left);
        HashSet<string> rightWords = SplitWords(right);

        if (leftWords.Count == 0 || rightWords.Count == 0)
        {
            return 0;
        }

        int overlap = leftWords.Intersect(rightWords).Count();
        return (double)overlap / Math.Min(leftWords.Count, rightWords.Count);
    }

    private static HashSet<string> SplitWords(string text)
    {
        return text.Split([' ', '\r', '\n', '\t', '.', ',', ';', ':', '!', '?', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class OcrTextGroup
    {
        private readonly List<string> _texts = [];
        private readonly List<string> _sourceEntryIds = [];

        public OcrTextGroup(MetadataEntry entry, string text)
        {
            StartTimestamp = entry.Timestamp;
            EndTimestamp = entry.Timestamp;
            Add(entry, text);
        }

        public long StartTimestamp { get; }

        public long EndTimestamp { get; private set; }

        public string Text => _texts[^1];

        public void Add(MetadataEntry entry, string text)
        {
            _texts.Add(text);
            _sourceEntryIds.Add(CreateSourceEntryId(entry));
            EndTimestamp = entry.Timestamp;
        }

        public InsightEntry ToInsight(string processorId)
        {
            string text = ChooseRepresentativeText();
            IReadOnlyList<string> uniqueTexts = _texts
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new InsightEntry(
                timestamp: StartTimestamp,
                category: "text-segment",
                processorId: processorId,
                title: Truncate(text, 80),
                description: text,
                duration: EndTimestamp > StartTimestamp ? EndTimestamp - StartTimestamp : null,
                tags: ["ocr", "text"],
                confidence: Math.Min(1.0, 0.65 + (_texts.Count * 0.05)),
                sourceEntryIds: _sourceEntryIds,
                additionalData: new Dictionary<string, object?>
                {
                    ["sampleCount"] = _texts.Count,
                    ["uniqueTextCount"] = uniqueTexts.Count,
                    ["texts"] = string.Join(" | ", uniqueTexts)
                });
        }

        private string ChooseRepresentativeText()
        {
            return _texts
                .OrderByDescending(text => SplitWords(text).Count)
                .ThenByDescending(text => text.Length)
                .First();
        }
    }

    private static string CreateSourceEntryId(MetadataEntry entry)
    {
        return $"{entry.ScannerId}@{entry.Timestamp}";
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "...";
    }
}
