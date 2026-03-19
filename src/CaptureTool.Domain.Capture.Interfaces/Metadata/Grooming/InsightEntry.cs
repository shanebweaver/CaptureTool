namespace CaptureTool.Domain.Capture.Interfaces.Metadata.Grooming;

/// <summary>
/// Represents a refined, high-level insight derived from one or more raw metadata entries.
/// Suitable for driving an enhanced playback user experience with meaningful timestamps.
/// </summary>
public sealed class InsightEntry
{
    /// <summary>
    /// Gets the position in the recording where this insight begins, in 100-nanosecond ticks.
    /// </summary>
    public long Timestamp { get; }

    /// <summary>
    /// Gets the duration of this insight in 100-nanosecond ticks.
    /// Null if the insight is a point-in-time event with no meaningful duration.
    /// </summary>
    public long? Duration { get; }

    /// <summary>
    /// Gets the category of this insight (e.g., "text-segment", "audio-activity", "scene-change").
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the groomer ID that produced this insight.
    /// </summary>
    public string GroomerId { get; }

    /// <summary>
    /// Gets a short, human-readable title for this insight.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets a detailed description of this insight.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets tags that can be used to filter or search insights.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Gets the confidence level of this insight, from 0.0 (uncertain) to 1.0 (certain).
    /// </summary>
    public double Confidence { get; }

    /// <summary>
    /// Gets the IDs of raw <see cref="MetadataEntry"/> items that contributed to this insight.
    /// </summary>
    public IReadOnlyList<string> SourceEntryIds { get; }

    /// <summary>
    /// Gets optional additional data associated with this insight.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? AdditionalData { get; }

    public InsightEntry(
        long timestamp,
        string category,
        string groomerId,
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
        GroomerId = groomerId ?? throw new ArgumentNullException(nameof(groomerId));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description;
        Duration = duration;
        Tags = tags ?? [];
        Confidence = Math.Clamp(confidence, 0.0, 1.0);
        SourceEntryIds = sourceEntryIds ?? [];
        AdditionalData = additionalData;
    }
}
