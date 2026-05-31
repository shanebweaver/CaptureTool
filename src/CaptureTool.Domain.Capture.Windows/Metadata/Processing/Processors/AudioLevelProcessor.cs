using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

namespace CaptureTool.Domain.Capture.Windows.Metadata.Processing.Processors;

public sealed class AudioLevelProcessor : IMetadataProcessor
{
    private const long MergeGapTicks = TimeSpan.TicksPerSecond / 2;
    private const int ActiveAudioFrameThreshold = 256;

    public string ProcessorId => "audio-level";

    public string Name => "Audio Level Processor";

    public IReadOnlyList<string> SupportedKeys { get; } = ["audio-info"];

    public Task<IReadOnlyList<InsightEntry>> ProcessAsync(
        IReadOnlyList<MetadataEntry> rawEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawEntries);

        List<AudioActivityGroup> groups = [];

        foreach (MetadataEntry entry in rawEntries
            .Where(entry => string.Equals(entry.Key, "audio-info", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Timestamp))
        {
            cancellationToken.ThrowIfCancellationRequested();

            int numFrames = GetIntValue(entry.AdditionalData, "numFrames");
            if (numFrames < ActiveAudioFrameThreshold)
            {
                continue;
            }

            AudioActivityGroup? currentGroup = groups.LastOrDefault();
            if (currentGroup is not null && entry.Timestamp - currentGroup.EndTimestamp <= MergeGapTicks)
            {
                currentGroup.Add(entry, numFrames);
            }
            else
            {
                groups.Add(new AudioActivityGroup(entry, numFrames));
            }
        }

        IReadOnlyList<InsightEntry> insights = groups
            .Select(group => group.ToInsight(ProcessorId))
            .OrderBy(insight => insight.Timestamp)
            .ToList();

        return Task.FromResult(insights);
    }

    private static int GetIntValue(IReadOnlyDictionary<string, object?>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out object? value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => longValue > int.MaxValue ? int.MaxValue : (int)longValue,
            double doubleValue => (int)doubleValue,
            string stringValue when int.TryParse(stringValue, out int parsedValue) => parsedValue,
            _ => 0
        };
    }

    private sealed class AudioActivityGroup
    {
        private readonly List<string> _sourceEntryIds = [];
        private int _sampleCount;
        private long _totalFrames;

        public AudioActivityGroup(MetadataEntry entry, int numFrames)
        {
            StartTimestamp = entry.Timestamp;
            EndTimestamp = entry.Timestamp;
            Add(entry, numFrames);
        }

        public long StartTimestamp { get; }

        public long EndTimestamp { get; private set; }

        public void Add(MetadataEntry entry, int numFrames)
        {
            EndTimestamp = entry.Timestamp;
            _sampleCount++;
            _totalFrames += numFrames;
            _sourceEntryIds.Add($"{entry.ScannerId}@{entry.Timestamp}");
        }

        public InsightEntry ToInsight(string processorId)
        {
            return new InsightEntry(
                timestamp: StartTimestamp,
                category: "audio-activity",
                processorId: processorId,
                title: "Audio activity",
                description: "Audio samples were detected during this segment.",
                duration: EndTimestamp > StartTimestamp ? EndTimestamp - StartTimestamp : null,
                tags: ["audio"],
                confidence: 0.8,
                sourceEntryIds: _sourceEntryIds,
                additionalData: new Dictionary<string, object?>
                {
                    ["sampleCount"] = _sampleCount,
                    ["totalFrames"] = _totalFrames
                });
        }
    }
}
