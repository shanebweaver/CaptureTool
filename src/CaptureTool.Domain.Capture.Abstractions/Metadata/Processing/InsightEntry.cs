namespace CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

/// <summary>
/// A high-level, timestamped insight derived from one or more raw metadata entries.
/// </summary>
public sealed class InsightEntry
{
    public long Timestamp { get; }

    public long? Duration { get; }

    public string Category { get; }

    public string ProcessorId { get; }

    public string Title { get; }

    public string? Description { get; }

    public IReadOnlyList<string> Tags { get; }

    public double Confidence { get; }

    public IReadOnlyList<string> SourceEntryIds { get; }

    public IReadOnlyDictionary<string, object?>? AdditionalData { get; }

    public InsightEntry(
        long timestamp,
        string category,
        string processorId,
        string title,
        string? description = null,
        long? duration = null,
        IReadOnlyList<string>? tags = null,
        double confidence = 1.0,
        IReadOnlyList<string>? sourceEntryIds = null,
        IReadOnlyDictionary<string, object?>? additionalData = null)
    {
        Timestamp = timestamp;
        Category = category ?? throw new ArgumentNullException(nameof(category));
        ProcessorId = processorId ?? throw new ArgumentNullException(nameof(processorId));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description;
        Duration = duration;
        Tags = tags ?? [];
        Confidence = Math.Clamp(confidence, 0.0, 1.0);
        SourceEntryIds = sourceEntryIds ?? [];
        AdditionalData = additionalData;
    }
}
