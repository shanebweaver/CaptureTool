namespace CaptureTool.Domain.Capture.Abstractions.Metadata.Processing;

/// <summary>
/// A processed metadata file containing playback-ready insights.
/// </summary>
public sealed class RefinedMetadataFile
{
    public const string FileExtension = ".insights.json";

    public string SourceFilePath { get; }

    public string? SourceMetadataFilePath { get; }

    public DateTime ProcessingTimestamp { get; }

    public IReadOnlyDictionary<string, string> ProcessorInfo { get; }

    public IReadOnlyList<InsightEntry> Insights { get; }

    public RefinedMetadataFile(
        string sourceFilePath,
        string? sourceMetadataFilePath,
        DateTime processingTimestamp,
        IReadOnlyList<InsightEntry> insights,
        IReadOnlyDictionary<string, string> processorInfo)
    {
        SourceFilePath = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
        SourceMetadataFilePath = sourceMetadataFilePath;
        ProcessingTimestamp = processingTimestamp;
        Insights = insights ?? throw new ArgumentNullException(nameof(insights));
        ProcessorInfo = processorInfo ?? throw new ArgumentNullException(nameof(processorInfo));
    }
}
