using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.ViewModels;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public enum CaptureTextSource
{
    ImageText,
    Speech,
    QrCode
}

public sealed record CaptureTextLocation(string Label, TimeSpan? Time, NormalizedBounds? Bounds);

public sealed class CaptureTextPassage : ViewModelBase
{
    private CaptureTextNavigationContext _context = new(false, false);
    private string _unavailable = string.Empty;

    public string Id { get; }

    public CaptureTextSource Source { get; }

    public string Label { get; }

    public string Text { get; }

    /// <summary>Original word bounds for image selection; passage bounds are only for navigation.</summary>
    public IReadOnlyList<RecognizedText> TextRegions { get; }

    public IReadOnlyList<IReadOnlyList<RecognizedText>> TextLines { get; }

    public ObservableCollection<CaptureTextLocation> Locations { get; }

    public bool HasOccurrences => Locations.Count > 1;

    public string Query
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public CaptureTextLocation? SelectedLocation
    {
        get;
        set
        {
            // Recycled two-way ComboBox bindings may briefly offer the previous row's selection.
            if (value == null ? Locations.Count > 0 : !Locations.Contains(value))
            {
                return;
            }

            if (Set(ref field, value))
            {
                UpdateNavigation();
                RaisePropertyChanged(nameof(IsTimed));
                RaisePropertyChanged(nameof(IsTimestampVisible));
                RaisePropertyChanged(nameof(NavigationLabel));
            }
        }
    }

    public bool IsTimed => SelectedLocation?.Time != null;

    public bool ShowTimestamp { get; init; } = true;

    public bool IsTimestampVisible => ShowTimestamp && IsTimed;

    public string NavigationLabel => IsTimed ? Label + " " + SelectedLocation!.Label : SelectedLocation?.Label ?? Label;

    public bool CanNavigate
    {
        get;
        private set => Set(ref field, value);
    }

    public string NavigationHint
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public Uri? WebUri
    {
        get
        {
            if (Source != CaptureTextSource.QrCode || !Uri.TryCreate(Text, UriKind.Absolute, out var uri))
            {
                return null;
            }

            return uri.Scheme is "http" or "https" && !string.IsNullOrEmpty(uri.Host) ? uri : null;
        }
    }

    public bool HasWebLink => WebUri != null;

    public CaptureTextPassage(
        string id,
        CaptureTextSource source,
        string label,
        string text,
        IEnumerable<CaptureTextLocation> locations,
        IReadOnlyList<RecognizedText>? textRegions = null,
        IReadOnlyList<IReadOnlyList<RecognizedText>>? textLines = null)
    {
        Id = id;
        Source = source;
        Label = label;
        Text = text;
        Locations = new(locations);
        TextRegions = textRegions ?? [];
        TextLines = textLines ?? [];
        SelectedLocation = Locations.FirstOrDefault();
    }

    public void SetNavigationContext(CaptureTextNavigationContext context, string unavailable)
    {
        _context = context;
        _unavailable = unavailable;
        UpdateNavigation();
    }

    private void UpdateNavigation()
    {
        CanNavigate = _context.IsReady && _context.SourceMatches && CanNavigateToSelectedLocation();
        NavigationHint = CanNavigate ? string.Empty : _unavailable;
    }

    private bool CanNavigateToSelectedLocation()
    {
        if (SelectedLocation is not { } location)
        {
            return false;
        }

        if (location.Time is { } time)
        {
            return time.TotalSeconds >= _context.StartSeconds &&
                (_context.EndSeconds == null || time.TotalSeconds <= _context.EndSeconds);
        }

        return location.Bounds is { Width: > 0, Height: > 0 } && !_context.ImageEdited;
    }

    internal bool SameContent(CaptureTextPassage other) =>
        Id == other.Id && Source == other.Source && Text == other.Text &&
        ShowTimestamp == other.ShowTimestamp &&
        Locations.SequenceEqual(other.Locations) && TextRegions.SequenceEqual(other.TextRegions);

    public static IReadOnlyList<CaptureTextPassage> From(
        RecognizedTextDocument document, ILocalizationService localization) =>
        CaptureTextPassageFactory.FromDocument(document, localization);

    public static IReadOnlyList<CaptureTextPassage> From(
        CaptureAnalysisRecord record, ILocalizationService localization) =>
        CaptureTextPassageFactory.FromRecord(record, localization);
}

public sealed record CaptureTextNavigationContext(
    bool IsReady,
    bool SourceMatches,
    bool ImageEdited = false,
    double StartSeconds = 0,
    double? EndSeconds = null);
