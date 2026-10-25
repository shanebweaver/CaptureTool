using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Abstractions.Analysis;

public sealed record MetadataTextEntry(Guid ResultId, AnalysisCapability Capability, int EntryIndex, string Text);

/// <summary>Whole text entries only: truncating a token could invent a fact. Original entry indexes are preserved.</summary>
public sealed class MetadataProcessorInput
{
    public CaptureId CaptureId { get; }
    public AnalysisMediaKind MediaKind { get; }
    public SourceRevision SourceRevision { get; }
    public MetadataProcessorDescriptor Descriptor { get; }
    public IReadOnlyList<AnalysisInputReference> Inputs { get; }
    public AnalysisDerivation? Derivation { get; }
    public IReadOnlyList<MetadataTextEntry> Entries { get; }
    public MetadataProcessingCoverage Coverage { get; }

    private MetadataProcessorInput(CaptureAnalysisRecord record, MetadataProcessorDescriptor descriptor,
        AnalysisInputReference[] inputs, List<MetadataTextEntry> entries, MetadataProcessingCoverage coverage)
    {
        CaptureId = record.CaptureId;
        MediaKind = record.MediaKind;
        SourceRevision = record.SourceRevision;
        Descriptor = descriptor;
        Inputs = Array.AsReadOnly(inputs);
        Derivation = inputs.Any(input => input.ResultId != null) ? new(inputs) : null;
        Entries = entries.AsReadOnly();
        Coverage = coverage;
    }

    public static MetadataProcessorInput Create(CaptureAnalysisRecord record, MetadataProcessorDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();
        AnalysisResult?[] sources = descriptor.Inputs.Select(capability =>
            record.Results.SingleOrDefault(result => result.Payload.Capability == capability)).ToArray();
        AnalysisInputReference[] references = descriptor.Inputs.Select((capability, index) => new AnalysisInputReference(capability, sources[index]?.ResultId)).ToArray();
        long available = sources.Sum(source => (long)EntryCount(source?.Payload));
        var selection = new EntrySelection(descriptor.Limits, cancellationToken);
        if (descriptor.InputSelection == MetadataInputSelection.Balanced)
        {
            selection.SelectBalanced(sources, available);
        }
        else
        {
            selection.SelectInOrder(sources);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var coverage = new MetadataProcessingCoverage(available, selection.Entries.Count,
            selection.CharacterCount, selection.AppliedLimits);
        return new(record, descriptor, references, selection.Entries, coverage);
    }

    private sealed class EntrySelection(MetadataProcessingLimits limits, CancellationToken cancellationToken)
    {
        public List<MetadataTextEntry> Entries { get; } = [];
        public int CharacterCount { get; private set; }
        public MetadataProcessingLimit AppliedLimits { get; private set; }

        public void SelectInOrder(AnalysisResult?[] sources)
        {
            int examined = 0;
            foreach (AnalysisResult source in sources.OfType<AnalysisResult>())
            {
                int count = EntryCount(source.Payload);
                for (int index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (examined == limits.MaxEntries)
                    {
                        AppliedLimits |= MetadataProcessingLimit.InputEntries;
                        return;
                    }

                    examined++;
                    string text = EntryText(source.Payload, index);
                    if (text.Length > limits.MaxEntryCharacters)
                    {
                        AppliedLimits |= MetadataProcessingLimit.OversizedEntry;
                        continue;
                    }

                    if (text.Length > limits.MaxCharacters - CharacterCount)
                    {
                        AppliedLimits |= MetadataProcessingLimit.InputCharacters;
                        return;
                    }

                    AddEntry(new(source.ResultId, source.Payload.Capability, index, text));
                }
            }
        }

        public void SelectBalanced(AnalysisResult?[] sources, long available)
        {
            AnalysisResult[] populated = sources.OfType<AnalysisResult>()
                .Where(source => EntryCount(source.Payload) > 0)
                .ToArray();
            if (populated.Length == 0)
            {
                return;
            }

            int[] counts = populated.Select(source => EntryCount(source.Payload)).ToArray();
            int slots = (int)Math.Min(available, limits.MaxEntries);
            int[] quotas = AllocateEntryQuotas(counts, slots);
            if (available > slots)
            {
                AppliedLimits |= MetadataProcessingLimit.InputEntries;
            }

            List<MetadataTextEntry>[] deferred = SelectReservedEntries(populated, counts, quotas);
            ShareRemainingCharacters(deferred);
            RestoreSourceOrder(populated);
        }

        private int[] AllocateEntryQuotas(int[] counts, int slots)
        {
            int[] quotas = new int[counts.Length];
            // Water-fill entry reservations: a short source gives unused slots to
            // longer sources before sampling. Examine at most MaxEntries in total.
            for (int assigned = 0; assigned < slots;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int source = 0; source < counts.Length && assigned < slots; source++)
                {
                    if (quotas[source] < counts[source])
                    {
                        quotas[source]++;
                        assigned++;
                    }
                }
            }

            return quotas;
        }

        private List<MetadataTextEntry>[] SelectReservedEntries(AnalysisResult[] populated, int[] counts, int[] quotas)
        {
            List<MetadataTextEntry>[] deferred = populated.Select(_ => new List<MetadataTextEntry>()).ToArray();
            for (int source = 0; source < populated.Length; source++)
            {
                int used = 0;
                int reserved = limits.MaxCharacters / populated.Length + (source < limits.MaxCharacters % populated.Length ? 1 : 0);
                foreach (int sample in SamplePriority(quotas[source]))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Include both ends and evenly spaced entries in between. All
                    // evidence keeps the original index, even when most is omitted.
                    int index = quotas[source] == 1
                        ? (counts[source] - 1) / 2
                        : (int)((long)sample * (counts[source] - 1) / (quotas[source] - 1));
                    string text = EntryText(populated[source].Payload, index);
                    if (text.Length > limits.MaxEntryCharacters)
                    {
                        AppliedLimits |= MetadataProcessingLimit.OversizedEntry;
                        continue;
                    }
                    var entry = new MetadataTextEntry(populated[source].ResultId, populated[source].Payload.Capability, index, text);
                    if (text.Length <= reserved - used)
                    {
                        AddEntry(entry);
                        used += text.Length;
                    }
                    else
                    {
                        deferred[source].Add(entry);
                    }
                }
            }

            return deferred;
        }

        private void ShareRemainingCharacters(List<MetadataTextEntry>[] deferred)
        {
            // Share unused character reservations round-robin, keeping the first
            // pass's guarantees even when an earlier source has very long entries.
            for (int round = 0; round < deferred.Max(items => items.Count); round++)
            {
                foreach (List<MetadataTextEntry> candidates in deferred)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (round >= candidates.Count)
                    {
                        continue;
                    }

                    MetadataTextEntry entry = candidates[round];
                    if (entry.Text.Length <= limits.MaxCharacters - CharacterCount)
                    {
                        AddEntry(entry);
                    }
                    else
                    {
                        AppliedLimits |= MetadataProcessingLimit.InputCharacters;
                    }
                }
            }
        }

        private void RestoreSourceOrder(AnalysisResult[] sources)
        {
            // Restore source/chronological order after sharing spare capacity.
            var order = sources.Select((source, index) => (source.ResultId, index))
                .ToDictionary(item => item.ResultId, item => item.index);
            Entries.Sort((left, right) => order[left.ResultId] != order[right.ResultId]
                ? order[left.ResultId].CompareTo(order[right.ResultId]) : left.EntryIndex.CompareTo(right.EntryIndex));
        }

        private void AddEntry(MetadataTextEntry entry)
        {
            Entries.Add(entry);
            CharacterCount += entry.Text.Length;
        }
    }

    private static IEnumerable<int> SamplePriority(int count)
    {
        if (count == 0)
        {
            yield break;
        }

        yield return 0;
        if (count == 1)
        {
            yield break;
        }

        yield return count - 1;
        // Select endpoints, then subdivide gaps. If characters run out before
        // entries do, the accepted samples still span the source's full range.
        Queue<(int Start, int End)> gaps = new();
        if (count > 2)
        {
            gaps.Enqueue((1, count - 2));
        }

        while (gaps.TryDequeue(out var gap))
        {
            int middle = gap.Start + (gap.End - gap.Start) / 2;
            yield return middle;
            if (gap.Start < middle)
            {
                gaps.Enqueue((gap.Start, middle - 1));
            }

            if (middle < gap.End)
            {
                gaps.Enqueue((middle + 1, gap.End));
            }
        }
    }

    private static int EntryCount(AnalysisPayload? payload) => payload switch
    {
        null => 0,
        TextRecognitionMetadata ocr => ocr.Regions.Count,
        QrCodeMetadata qr => qr.Codes.Count,
        TranscriptMetadata transcript => transcript.Segments.Count,
        DescriptionMetadata descriptions => descriptions.Descriptions.Count,
        _ => throw new ArgumentException("Unsupported metadata input payload.", nameof(payload)),
    };

    private static string EntryText(AnalysisPayload payload, int index) => payload switch
    {
        TextRecognitionMetadata ocr => ocr.Regions[index].Text,
        QrCodeMetadata qr => qr.Codes[index].Value,
        TranscriptMetadata transcript => transcript.Segments[index].Text,
        DescriptionMetadata descriptions => descriptions.Descriptions[index].Text,
        _ => throw new ArgumentException("Unsupported metadata input payload.", nameof(payload)),
    };
}
