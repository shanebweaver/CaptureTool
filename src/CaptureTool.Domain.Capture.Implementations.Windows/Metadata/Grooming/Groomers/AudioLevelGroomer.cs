using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

namespace CaptureTool.Domain.Capture.Implementations.Windows.Metadata.Grooming.Groomers;

/// <summary>
/// Groomer that analyzes raw audio-info metadata entries and identifies periods of
/// significant audio activity versus silence, producing "audio-activity" and
/// "audio-silence" insights useful for an enhanced playback experience.
/// Requires audio scanners that emit entries with key "audio-info" and
/// an "numFrames" value in AdditionalData to function; entries without this
/// data are treated as silence markers.
/// </summary>
public sealed class AudioLevelGroomer : IMetadataGroomer
{
    // Minimum number of audio frames to be classified as "active" audio
    private const int ActiveFrameThreshold = 256;

    // Time gap after which a new activity segment begins (0.5 s in 100ns ticks)
    private const long ActivityGapTicks = 5_000_000L;

    public string GroomerId => "audio-level";
    public string Name => "Audio Level Groomer";
    public IReadOnlyList<string> SupportedKeys => ["audio-info"];

    public Task<IReadOnlyList<InsightEntry>> GroomAsync(
        IReadOnlyList<MetadataEntry> rawEntries,
        CancellationToken cancellationToken = default)
    {
        var results = new List<InsightEntry>();

        if (rawEntries.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<InsightEntry>>(results);
        }

        var sorted = rawEntries.OrderBy(e => e.Timestamp).ToList();

        // Partition into active and silent samples
        var activeSamples = sorted
            .Where(e => GetNumFrames(e) >= ActiveFrameThreshold)
            .ToList();

        if (activeSamples.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<InsightEntry>>(results);
        }

        // Group consecutive active samples that are close together in time
        var currentGroup = new List<MetadataEntry> { activeSamples[0] };

        for (int i = 1; i < activeSamples.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long gap = activeSamples[i].Timestamp - activeSamples[i - 1].Timestamp;
            if (gap <= ActivityGapTicks)
            {
                currentGroup.Add(activeSamples[i]);
            }
            else
            {
                results.Add(BuildActivityInsight(currentGroup));
                currentGroup = [activeSamples[i]];
            }
        }

        if (currentGroup.Count > 0)
        {
            results.Add(BuildActivityInsight(currentGroup));
        }

        return Task.FromResult<IReadOnlyList<InsightEntry>>(results);
    }

    private InsightEntry BuildActivityInsight(List<MetadataEntry> group)
    {
        var first = group[0];
        var last = group[^1];
        long duration = last.Timestamp - first.Timestamp;

        // Summarize audio format from the first entry
        string audioFormat = first.Value?.ToString() ?? "audio";

        return new InsightEntry(
            timestamp: first.Timestamp,
            category: "audio-activity",
            groomerId: GroomerId,
            title: $"Audio activity ({audioFormat})",
            description: $"Audio detected across {group.Count} samples",
            duration: duration > 0 ? duration : null,
            tags: ["audio", "activity"],
            confidence: Math.Min(1.0, 0.6 + (group.Count * 0.02)),
            sourceEntryIds: group.Select(e => $"{e.ScannerId}@{e.Timestamp}").ToList(),
            additionalData: new Dictionary<string, object?>
            {
                ["sampleCount"] = group.Count,
                ["audioFormat"] = audioFormat
            });
    }

    private static int GetNumFrames(MetadataEntry entry)
    {
        if (entry.AdditionalData != null &&
            entry.AdditionalData.TryGetValue("numFrames", out var raw) &&
            raw is int frames)
        {
            return frames;
        }

        return 0;
    }
}
