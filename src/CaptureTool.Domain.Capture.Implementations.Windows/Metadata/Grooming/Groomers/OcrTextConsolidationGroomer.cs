using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Grooming.Groomers;

/// <summary>
/// Groomer that consolidates raw OCR text entries produced by the Windows OCR scanner
/// into coherent text-segment insights with merged content and meaningful duration.
/// Consecutive entries within a similarity threshold and time gap are merged into a
/// single insight, providing a clean text timeline for enhanced playback.
/// </summary>
public sealed class OcrTextConsolidationGroomer : IMetadataGroomer
{
    // Group entries within this time window (1 second in 100ns ticks)
    private const long MaxGapTicks = 10_000_000L;

    // Minimum text length to consider for an insight
    private const int MinTextLength = 3;

    public string GroomerId => "ocr-text-consolidation";
    public string Name => "OCR Text Consolidation Groomer";
    public IReadOnlyList<string> SupportedKeys => ["ocr-text"];

    public Task<IReadOnlyList<InsightEntry>> GroomAsync(
        IReadOnlyList<MetadataEntry> rawEntries,
        CancellationToken cancellationToken = default)
    {
        var results = new List<InsightEntry>();

        if (rawEntries.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<InsightEntry>>(results);
        }

        // Sort by timestamp
        var sorted = rawEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Value?.ToString()) &&
                        (e.Value?.ToString()?.Length ?? 0) >= MinTextLength)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (sorted.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<InsightEntry>>(results);
        }

        // Group consecutive entries that are close in time and share similar content
        var currentGroup = new List<MetadataEntry> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var prev = sorted[i - 1];
            var curr = sorted[i];
            long gap = curr.Timestamp - prev.Timestamp;

            if (gap <= MaxGapTicks && IsSimilarText(prev.Value?.ToString(), curr.Value?.ToString()))
            {
                currentGroup.Add(curr);
            }
            else
            {
                results.Add(BuildInsight(currentGroup));
                currentGroup = [curr];
            }
        }

        // Flush last group
        if (currentGroup.Count > 0)
        {
            results.Add(BuildInsight(currentGroup));
        }

        return Task.FromResult<IReadOnlyList<InsightEntry>>(results);
    }

    private InsightEntry BuildInsight(List<MetadataEntry> group)
    {
        var firstEntry = group[0];
        var lastEntry = group[^1];

        // Use the most-repeated text as the representative value
        string representativeText = group
            .Select(e => e.Value?.ToString() ?? string.Empty)
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        long duration = lastEntry.Timestamp - firstEntry.Timestamp;

        // Build a short title from the first ~60 chars of the representative text
        string title = representativeText.Length > 60
            ? representativeText[..57] + "..."
            : representativeText;

        var sourceIds = group
            .Select((e, idx) => $"{e.ScannerId}@{e.Timestamp}")
            .ToList();

        return new InsightEntry(
            timestamp: firstEntry.Timestamp,
            category: "text-segment",
            groomerId: GroomerId,
            title: title,
            description: representativeText,
            duration: duration > 0 ? duration : null,
            tags: ["ocr", "text"],
            confidence: ComputeConfidence(group),
            sourceEntryIds: sourceIds,
            additionalData: new Dictionary<string, object?>
            {
                ["entryCount"] = group.Count,
                ["wordCount"] = representativeText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
            });
    }

    /// <summary>
    /// Returns true when two text values share enough common words to be considered the same on-screen content.
    /// </summary>
    private static bool IsSimilarText(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        // Simple word-overlap similarity: if >40% of words match, treat as similar
        var wordsA = new HashSet<string>(
            a.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (wordsA.Count == 0 || wordsB.Length == 0)
        {
            return false;
        }

        int overlap = wordsB.Count(w => wordsA.Contains(w));
        double similarity = (double)overlap / Math.Max(wordsA.Count, wordsB.Length);
        return similarity >= 0.4;
    }

    private static double ComputeConfidence(List<MetadataEntry> group)
    {
        // More entries in the group → higher confidence (capped at 1.0)
        return Math.Min(1.0, 0.5 + (group.Count * 0.1));
    }
}
