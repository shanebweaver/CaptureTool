using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Globalization;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>A focused read projection. Raw facts, classification and provider diagnostics stay out of the pane.</summary>
public sealed class CaptureDetailsContent
{
    public string Summary { get; private init; } = string.Empty;
    public IReadOnlyList<CaptureTextPassage> Passages { get; private init; } = [];
    public bool HasLimitedCoverage { get; private init; }
    public bool HasRetainedResults { get; private init; }
    public bool HasContent => Summary.Length > 0 || Passages.Count > 0;

    public static CaptureDetailsContent Create(CaptureAnalysisRecord record, ILocalizationService localization) => new()
    {
        Summary = string.Join(" ", record.Results.Select(result => result.Payload).OfType<CaptureSynopsisMetadata>()
            .SelectMany(synopsis => synopsis.Summary).Select(statement => statement.Text)),
        Passages = CaptureTextPassage.From(record, localization),
        HasLimitedCoverage = record.Results.Any(result => result.Payload is CaptureSynopsisMetadata { Coverage.IsComplete: false }) ||
            record.MediaKind == AnalysisMediaKind.Video && record.Results.Any(result => result.Payload is TextRecognitionMetadata),
        HasRetainedResults = record.Results.Any(result => result.ProducingRunId is { } producingRun && producingRun != record.RunId)
    };

    internal static string Time(TimeSpan time) => time.TotalHours >= 1
        ? $"{(long)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    internal static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString(unit == 0 ? "N0" : "0.##", CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
