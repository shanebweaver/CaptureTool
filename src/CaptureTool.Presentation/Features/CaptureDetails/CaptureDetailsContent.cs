using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Globalization;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed record CaptureDetailEvidence(string Label, string Text);
public sealed class CaptureDetailItem(string label, string text, IEnumerable<CaptureDetailEvidence> evidence)
{
    public string Label { get; } = label;
    public string Text { get; } = text;
    public ObservableCollection<CaptureDetailEvidence> Evidence { get; } = new(evidence);
    public bool HasEvidence => Evidence.Count > 0;
}
public sealed record CaptureDetailSection(string Title, ObservableCollection<CaptureDetailItem> Items, string CopyText);

/// <summary>Presentation of normalized payloads; provider response formats never reach the view.</summary>
public sealed class CaptureDetailsContent
{
    // Use the same bindable collection shape as the app's other Native AOT ItemsSource consumers.
    public ObservableCollection<CaptureDetailItem> Overview { get; private init; } = [];
    public ObservableCollection<CaptureDetailItem> Facts { get; private init; } = [];
    public ObservableCollection<CaptureDetailSection> Sources { get; private init; } = [];
    public ObservableCollection<CaptureDetailItem> Properties { get; private init; } = [];
    public bool HasLimitedCoverage { get; private init; }
    public bool HasRetainedResults { get; private init; }
    public bool HasOverview => Overview.Count > 0;
    public bool HasFacts => Facts.Count > 0;
    public bool HasSources => Sources.Count > 0;
    public bool HasProperties => Properties.Count > 0;
    public bool HasContent => HasOverview || HasFacts || HasSources || HasProperties;

    public static CaptureDetailsContent Create(CaptureAnalysisRecord record, ILocalizationService localization)
    {
        string Text(string name) => localization.GetString("CaptureDetails_" + name);
        var overview = new List<CaptureDetailItem>();
        var facts = new List<CaptureDetailItem>();
        var sources = new List<CaptureDetailSection>();
        var properties = new List<CaptureDetailItem>();
        bool limited = false;
        foreach (AnalysisResult result in record.Results)
        {
            switch (result.Payload)
            {
                case CaptureSynopsisMetadata synopsis:
                    if (synopsis.Title != null) Suggestion("SuggestedTitle", synopsis.Title);
                    foreach (SuggestedText statement in synopsis.Summary) Suggestion("Summary", statement);
                    limited |= !synopsis.Coverage.IsComplete;
                    break;
                case CaptureClassificationMetadata classification:
                    if (classification.Category is { } category)
                        overview.Add(new(Text("Category"), Text("Category_" + category), Evidence(classification.CategoryEvidence)));
                    foreach (SuggestedText topic in classification.Topics) Suggestion("Topic", topic);
                    limited |= !classification.Coverage.IsComplete;
                    break;
                case StructuredFactsMetadata extracted:
                    foreach (StructuredFact fact in extracted.Facts)
                        facts.Add(new(Text("Fact_" + fact.Kind), fact.Value, Evidence(fact.Evidence)));
                    limited |= extracted.Coverage?.IsComplete == false;
                    break;
                case QrCodeMetadata qr:
                    foreach (var group in qr.Codes.Select((code, index) => (code, index)).GroupBy(item => item.code.Value))
                        facts.Add(new(Text("QrCode"), group.Key, group.Select(item =>
                            new CaptureDetailEvidence(SourceLabel(result.Payload, item.index), item.code.Value)).ToArray()));
                    break;
                case TextRecognitionMetadata ocr:
                    Source("RecognizedText", ocr.Regions.Select((item, index) =>
                        new CaptureDetailItem(SourceLabel(result.Payload, index), item.Text, [])).ToArray());
                    break;
                case TranscriptMetadata transcript:
                    Source("Transcript", transcript.Segments.Select((item, index) =>
                        new CaptureDetailItem(SourceLabel(result.Payload, index), item.Text, [])).ToArray());
                    break;
                case DescriptionMetadata descriptions:
                    Source("Descriptions", descriptions.Descriptions.Select((item, index) =>
                        new CaptureDetailItem(SourceLabel(result.Payload, index), item.Text, [])).ToArray());
                    break;
                case FileDetailsMetadata file:
                    Property("AnalyzedFile", file.FileName);
                    Property("AnalyzedAt", Date(result.GeneratedAt));
                    Property("MediaType", Text("Media_" + file.MediaKind));
                    Property("Size", FormatSize(file.SizeBytes));
                    Property("ContentType", file.ContentType);
                    Property("CapturedAt", file.CapturedAt is { } captured ? Date(captured) : Text("Unknown"));
                    Property("CreatedAt", Date(file.FileCreatedAt));
                    Property("ModifiedAt", Date(file.FileModifiedAt));
                    if (file.Duration is { } duration) Property("Duration", Time(duration));
                    if ((file.Image?.Dimensions ?? file.Video?.Dimensions) is { } dimensions)
                    {
                        Property("Dimensions", $"{dimensions.Width} × {dimensions.Height}");
                        uint divisor = Gcd(dimensions.Width, dimensions.Height);
                        Property("AspectRatio", $"{dimensions.Width / divisor}:{dimensions.Height / divisor}");
                    }
                    if (file.Image is { } image)
                    {
                        if (image.DpiX is { } dpiX) Property("DpiX", dpiX.ToString("0.##", CultureInfo.CurrentCulture));
                        if (image.DpiY is { } dpiY) Property("DpiY", dpiY.ToString("0.##", CultureInfo.CurrentCulture));
                    }
                    if (file.Video is { } video)
                    {
                        if (video.FrameRate is { } rate) Property("FrameRate", rate.ToString("0.##", CultureInfo.CurrentCulture));
                        if (video.Bitrate is { } bitrate) Property("VideoBitrate", bitrate.ToString("N0", CultureInfo.CurrentCulture) + " bps");
                        Property("VideoCodec", video.Codec);
                    }
                    if (file.Audio is { } audio)
                    {
                        if (audio.Channels is { } channels) Property("Channels", channels.ToString(CultureInfo.CurrentCulture));
                        if (audio.SampleRate is { } rate) Property("SampleRate", rate.ToString("N0", CultureInfo.CurrentCulture) + " Hz");
                        if (audio.Bitrate is { } bitrate) Property("AudioBitrate", bitrate.ToString("N0", CultureInfo.CurrentCulture) + " bps");
                        Property("AudioCodec", audio.Codec);
                    }
                    break;
            }
        }
        return new()
        {
            Overview = new(overview), Facts = new(facts), Sources = new(sources),
            Properties = new(properties), HasLimitedCoverage = limited,
            HasRetainedResults = record.Results.Any(result => result.ProducingRunId is { } producingRun && producingRun != record.RunId)
        };

        void Suggestion(string label, SuggestedText suggestion) => overview.Add(new(Text(label), suggestion.Text, Evidence(suggestion.Evidence)));
        void Source(string label, CaptureDetailItem[] items)
        {
            if (items.Length > 0) sources.Add(new(Text(label), new(items), string.Join(Environment.NewLine, items.Select(item => item.Text))));
        }
        void Property(string label, string? value)
        {
            if (!string.IsNullOrEmpty(value)) properties.Add(new(Text(label), value, []));
        }
        IReadOnlyList<CaptureDetailEvidence> Evidence(IReadOnlyList<AnalysisEvidence> evidence) => evidence
            .Select(span =>
            {
                AnalysisResult source = record.Results.Single(result => result.ResultId == span.ResultId);
                // Show the source entry for context, not just the extracted value repeated back to the user.
                string entry = source.Payload switch
                {
                    TextRecognitionMetadata ocr => ocr.Regions[span.EntryIndex].Text,
                    TranscriptMetadata transcript => transcript.Segments[span.EntryIndex].Text,
                    DescriptionMetadata descriptions => descriptions.Descriptions[span.EntryIndex].Text,
                    QrCodeMetadata qr => qr.Codes[span.EntryIndex].Value,
                    _ => span.ResolveText(source)
                };
                return new CaptureDetailEvidence(SourceLabel(source.Payload, span.EntryIndex), entry);
            }).Distinct().ToArray();
        string SourceLabel(AnalysisPayload payload, int index)
        {
            (string key, TimeSpan? timestamp) = payload switch
            {
                TextRecognitionMetadata ocr => ("RecognizedText", ocr.Regions[index].Timestamp),
                TranscriptMetadata transcript => ("Transcript", transcript.Segments[index].Start),
                DescriptionMetadata descriptions => ("Descriptions", descriptions.Descriptions[index].Timestamp),
                QrCodeMetadata qr => ("QrCode", qr.Codes[index].Timestamp),
                _ => ("Sources", null)
            };
            return timestamp is { } time ? $"{Text(key)} · {Time(time)}" : $"{Text(key)} · {index + 1}";
        }
    }

    private static string Date(DateTimeOffset date) => date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    internal static string Time(TimeSpan time) => time.TotalHours >= 1
        ? $"{(long)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    private static uint Gcd(uint a, uint b) { while (b != 0) (a, b) = (b, a % b); return a; }
    internal static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString(unit == 0 ? "N0" : "0.##", CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
