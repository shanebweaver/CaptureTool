using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Globalization;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>A focused read projection. Raw facts, classification and provider diagnostics stay out of the pane.</summary>
public sealed class CaptureDetailsContent
{
    public string AltText { get; private init; } = string.Empty;

    public string Summary { get; private init; } = string.Empty;

    public bool IsSummaryPartial { get; private init; }

    public string SuggestedName { get; private init; } = string.Empty;

    public string Description { get; private init; } = string.Empty;

    public bool HasSummaryInputs => Description.Length > 0 || Passages.Any(passage => !string.IsNullOrWhiteSpace(passage.Text));

    public IReadOnlyList<CaptureTextPassage> Passages { get; private init; } = [];

    public bool HasContent => AltText.Length > 0 || Summary.Length > 0 || Description.Length > 0 || Passages.Count > 0;

    public static CaptureDetailsContent Create(CaptureAnalysisRecord record, ILocalizationService localization)
    {
        var payloads = record.Results.Select(result => result.Payload).ToArray();
        var altText = payloads.OfType<ImageAltTextMetadata>().SingleOrDefault();
        var synopsis = payloads.OfType<CaptureSynopsisMetadata>().SingleOrDefault();
        var name = payloads.OfType<CaptureNameMetadata>().SingleOrDefault();
        var descriptions = payloads.OfType<DescriptionMetadata>()
            .SelectMany(payload => payload.Descriptions)
            .Select(FormatDescription);

        return new CaptureDetailsContent
        {
            AltText = altText?.Suggestion?.Text ?? string.Empty,
            Summary = string.Join(" ", synopsis?.Summary.Select(statement => statement.Text) ?? []),
            IsSummaryPartial = synopsis is { Summary.Count: > 0, Coverage.IsComplete: false },
            SuggestedName = name?.Suggestion?.Text ?? string.Empty,
            Description = string.Join(Environment.NewLine, descriptions),
            Passages = CaptureTextPassage.From(record, localization)
        };
    }

    private static string FormatDescription(MediaDescription description) =>
        description.Timestamp is { } time ? $"{Time(time)}  {description.Text}" : description.Text;

    internal static string Time(TimeSpan time) => time.TotalHours >= 1
        ? $"{(long)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{time.Minutes}:{time.Seconds:00}";

    internal static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(unit == 0 ? "N0" : "0.##", CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
