using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.ViewModels;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public enum CaptureTextSource { ImageText, Speech, QrCode }
public sealed record CaptureTextLocation(string Label, TimeSpan? Time, NormalizedBounds? Bounds);

public sealed class CaptureTextPassage : ViewModelBase
{
    public string Id { get; }
    public CaptureTextSource Source { get; }
    public string Label { get; }
    public string Text { get; }
    /// <summary>Original word bounds for image selection; passage bounds are only for navigation.</summary>
    public IReadOnlyList<RecognizedText> TextRegions { get; }
    public IReadOnlyList<IReadOnlyList<RecognizedText>> TextLines { get; }
    public ObservableCollection<CaptureTextLocation> Locations { get; }
    public bool HasOccurrences => Locations.Count > 1;
    public string Query { get; set => Set(ref field, value); } = string.Empty;
    public CaptureTextLocation? SelectedLocation
    {
        get;
        set
        {
            // Recycled two-way ComboBox bindings may briefly offer the previous row's selection.
            if (value == null ? Locations.Count > 0 : !Locations.Contains(value)) return;
            if (Set(ref field, value))
            {
                UpdateNavigation();
                RaisePropertyChanged(nameof(IsTimed));
                RaisePropertyChanged(nameof(NavigationLabel));
            }
        }
    }
    public bool IsTimed => SelectedLocation?.Time != null;
    public string NavigationLabel => IsTimed ? Label + " " + SelectedLocation!.Label : SelectedLocation?.Label ?? Label;
    public bool CanNavigate { get; private set => Set(ref field, value); }
    public string NavigationHint { get; private set => Set(ref field, value); } = string.Empty;
    public Uri? WebUri => Source == CaptureTextSource.QrCode && Uri.TryCreate(Text, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host) ? uri : null;
    public bool HasWebLink => WebUri != null;
    private CaptureTextNavigationContext _context = new(false, false);
    private string _unavailable = string.Empty;

    public CaptureTextPassage(string id, CaptureTextSource source, string label, string text, IEnumerable<CaptureTextLocation> locations,
        IReadOnlyList<RecognizedText>? textRegions = null, IReadOnlyList<IReadOnlyList<RecognizedText>>? textLines = null)
    {
        Id = id; Source = source; Label = label; Text = text; Locations = new(locations);
        TextRegions = textRegions ?? [];
        TextLines = textLines ?? [];
        SelectedLocation = Locations.FirstOrDefault();
    }

    public void SetNavigationContext(CaptureTextNavigationContext context, string unavailable)
    {
        _context = context; _unavailable = unavailable; UpdateNavigation();
    }

    private void UpdateNavigation()
    {
        CanNavigate = _context.IsReady && _context.SourceMatches && SelectedLocation is { } location &&
            (location.Time is { } time ? time.TotalSeconds >= _context.StartSeconds &&
                (_context.EndSeconds == null || time.TotalSeconds <= _context.EndSeconds) :
                location.Bounds is { Width: > 0, Height: > 0 } && !_context.ImageEdited);
        NavigationHint = CanNavigate ? string.Empty : _unavailable;
    }

    internal bool SameContent(CaptureTextPassage other) => Id == other.Id && Source == other.Source &&
        Text == other.Text && Locations.SequenceEqual(other.Locations) && TextRegions.SequenceEqual(other.TextRegions);

    public static IReadOnlyList<CaptureTextPassage> From(RecognizedTextDocument document, ILocalizationService localization)
    {
        string textLabel = localization.GetString("CaptureDetails_RecognizedText");
        string qrLabel = localization.GetString("CaptureDetails_QrCode");
        NormalizedBounds? Normalize(System.Drawing.RectangleF bounds)
        {
            if (document.ImageSize.Width <= 0 || document.ImageSize.Height <= 0) return null;
            var clipped = System.Drawing.RectangleF.Intersect(bounds, new(0, 0, document.ImageSize.Width, document.ImageSize.Height));
            if (clipped.Width <= 0 || clipped.Height <= 0) return null;
            double x = (double)clipped.X / document.ImageSize.Width, y = (double)clipped.Y / document.ImageSize.Height;
            return new(x, y, Math.Min(1 - x, (double)clipped.Width / document.ImageSize.Width),
                Math.Min(1 - y, (double)clipped.Height / document.ImageSize.Height));
        }
        var regions = document.Regions.Select(region => new RecognizedText(region.Text, Normalize(region.Bounds),
            lineIndex: region.LineIndex >= 0 ? region.LineIndex : null, wordIndex: region.WordIndex >= 0 ? region.WordIndex : null)).ToArray();
        var passages = CaptureTextGrouping.Create(regions).Select(group => new CaptureTextPassage($"editor:text:{group.FirstIndex}",
            CaptureTextSource.ImageText, textLabel, group.Text, [new(textLabel, null, group.Bounds)], group.Regions, group.Lines)).ToList();
        if (regions.Length == 0 && document.QrCodes.Count == 0 && !string.IsNullOrWhiteSpace(document.Text))
            passages.Add(new("editor:text", CaptureTextSource.ImageText, textLabel, document.Text, []));
        int index = 0;
        foreach (var codes in document.QrCodes.GroupBy(code => code.Value, StringComparer.Ordinal))
            passages.Add(new($"editor:qr:{index++}", CaptureTextSource.QrCode, qrLabel, codes.Key,
                codes.Select(code => new CaptureTextLocation(qrLabel, null, Normalize(code.Bounds)))));
        return passages;
    }

    public static IReadOnlyList<CaptureTextPassage> From(CaptureAnalysisRecord record, ILocalizationService localization)
    {
        string Label(string key) => localization.GetString("CaptureDetails_" + key);
        var passages = new List<CaptureTextPassage>();
        foreach (var result in record.Results)
        {
            switch (result.Payload)
            {
                case TextRecognitionMetadata text when record.MediaKind == AnalysisMediaKind.Video:
                    CaptureTextPassage? previous = null;
                    foreach (var frame in text.Regions.Select((region, index) => (region, index))
                        .GroupBy(item => item.region.Timestamp).OrderBy(group => group.Key))
                    {
                        string content = string.Join(Environment.NewLine + Environment.NewLine,
                            CaptureTextGrouping.Create(frame.Select(item => item.region).ToArray()).Select(group => group.Text));
                        var location = Location(frame.Key, null, Label("RecognizedText"));
                        if (previous?.Text == content && frame.Key != null) previous.Locations.Add(location);
                        else
                        {
                            previous = new($"{result.ResultId}:{frame.First().index}", CaptureTextSource.ImageText,
                                Label("RecognizedText"), content, [location]);
                            passages.Add(previous);
                        }
                    }
                    break;
                case TextRecognitionMetadata text:
                    passages.AddRange(CaptureTextGrouping.Create(text.Regions).Select(group => new CaptureTextPassage($"{result.ResultId}:{group.FirstIndex}",
                        CaptureTextSource.ImageText, Label("RecognizedText"), group.Text,
                        [Location(group.Regions[0].Timestamp, group.Bounds, Label("RecognizedText"))], group.Regions, group.Lines)));
                    break;
                case TranscriptMetadata transcript:
                    passages.AddRange(transcript.Segments.Select((segment, index) => new CaptureTextPassage($"{result.ResultId}:{index}",
                        CaptureTextSource.Speech, Label("Transcript"), segment.Text,
                        [Location(segment.Start, null, Label("Transcript"))])));
                    break;
                case QrCodeMetadata qr:
                    foreach (var group in qr.Codes.Select((code, index) => (code, index)).GroupBy(item => item.code.Value, StringComparer.Ordinal))
                        passages.Add(new($"{result.ResultId}:{group.First().index}", CaptureTextSource.QrCode, Label("QrCode"), group.Key,
                            group.Select(item => Location(item.code.Timestamp, record.MediaKind == AnalysisMediaKind.Image ? item.code.Bounds : null, Label("QrCode")))));
                    break;
            }
        }
        return passages.OrderBy(passage => passage.SelectedLocation?.Time ?? TimeSpan.Zero).ToArray();
    }

    private static CaptureTextLocation Location(TimeSpan? time, NormalizedBounds? bounds, string fallback) =>
        new(time is { } value ? CaptureDetailsContent.Time(value) : fallback, time, bounds);
}

public sealed record CaptureTextNavigationContext(bool IsReady, bool SourceMatches, bool ImageEdited = false,
    double StartSeconds = 0, double? EndSeconds = null);
