using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed class CaptureVideoTextFrame(TimeSpan? time, IReadOnlyList<CaptureTextPassage> passages, ILocalizationService text)
{
    public TimeSpan? Time { get; } = time;
    public IReadOnlyList<CaptureTextPassage> Passages { get; } = passages;
    public string Label { get; } = time is { } value
        ? CaptureDetailsContent.Time(value) + (value.Ticks % TimeSpan.TicksPerSecond == 0 ? string.Empty :
            "." + value.Milliseconds.ToString("D3", CultureInfo.InvariantCulture))
        : text.GetString("CaptureVideoText_UnknownTime");
    public string Preview { get; } = CreatePreview(passages);
    public string CountText { get; } = string.Format(CultureInfo.CurrentCulture, text.GetString("CapturePane_PassageCount"), passages.Count);

    private static string CreatePreview(IReadOnlyList<CaptureTextPassage> passages)
    {
        string preview = (passages.FirstOrDefault(passage => passage.Source == CaptureTextSource.ImageText) ?? passages.FirstOrDefault())
            ?.Text.ReplaceLineEndings(" ") ?? string.Empty;
        return preview.Length <= 160 ? preview : preview[..160] + "…";
    }
}

/// <summary>Searches video moments, then exposes one moment through the same text browser used for images.</summary>
public sealed class CaptureVideoTextViewModel : ViewModelBase
{
    private const int PageSize = 100;
    private readonly ILocalizationService _text;
    private IReadOnlyList<CaptureTextPassage> _source = [];
    private CaptureVideoTextFrame[] _frames = [];
    private CaptureVideoTextFrame[] _filtered = [];
    private CaptureTextNavigationContext _context = new(false, false);
    public ObservableCollection<CaptureVideoTextFrame> Visible { get; } = [];
    public CaptureTextViewModel Content { get; }
    public string Query { get; set { if (Set(ref field, value)) Filter(); } } = string.Empty;
    public CaptureVideoTextFrame? SelectedFrame
    {
        get;
        private set { if (Set(ref field, value)) RaisePropertyChanged(nameof(HasSelectedFrame)); }
    }
    public bool HasSelectedFrame => SelectedFrame != null;
    public bool HasMore => Visible.Count < _filtered.Length;
    public bool HasNoMatches => _filtered.Length == 0;
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;
    public IRelayCommand BackCommand { get; }
    public IRelayCommand LoadMoreCommand { get; }

    public CaptureVideoTextViewModel(ILocalizationService text)
    {
        _text = text;
        Content = new(text);
        BackCommand = new RelayCommand(() => { SelectedFrame = null; Content.SelectedPassage = null; });
        LoadMoreCommand = new RelayCommand(LoadMore, () => HasMore);
    }

    public void Replace(IReadOnlyList<CaptureTextPassage> passages)
    {
        if (ReferenceEquals(_source, passages)) return;
        _source = passages;
        // A QR value can occur at several times. Each moment gets its own locations and selection.
        _frames = passages.Where(passage => passage.Source != CaptureTextSource.Speech)
            .SelectMany(passage => passage.Locations.Count == 0
                ? new[] { (Passage: passage, Time: (TimeSpan?)null) }
                : passage.Locations.Select(location => (Passage: passage, location.Time)).Distinct())
            .GroupBy(item => item.Time).OrderBy(group => group.Key == null).ThenBy(group => group.Key)
            .Select(group => new CaptureVideoTextFrame(group.Key, group.Select(item => new CaptureTextPassage(
                item.Passage.Id, item.Passage.Source, item.Passage.Label, item.Passage.Text,
                item.Passage.Locations.Where(location => location.Time == group.Key)
                    .Select(location => group.Key == null ? location with { Bounds = null } : location), item.Passage.TextRegions, item.Passage.TextLines)
                { ShowTimestamp = false }).ToArray(), _text))
            .ToArray();
        Filter();
        if (SelectedFrame is { } selected)
        {
            var replacement = _frames.FirstOrDefault(frame => frame.Time == selected.Time);
            Content.Replace(replacement?.Passages ?? []);
            SelectedFrame = replacement;
        }
        SetNavigationContext(_context);
    }

    public void Open(CaptureVideoTextFrame frame)
    {
        if (!_filtered.Contains(frame)) return;
        Content.Query = string.Empty;
        Content.SelectedFilter = Content.Filters[0];
        Content.Replace(frame.Passages);
        Content.SetNavigationContext(_context);
        SelectedFrame = frame;
        // A timestamp query selects a moment; a text query carries through to its matching words.
        string query = Query.Trim();
        Content.Query = frame.Passages.Any(passage => passage.Text.Contains(query, StringComparison.OrdinalIgnoreCase)) ? query : string.Empty;
        Content.SelectedPassage = Content.Visible.FirstOrDefault();
    }

    public void SetNavigationContext(CaptureTextNavigationContext context)
    {
        _context = context;
        Content.SetNavigationContext(context);
    }

    private void Filter()
    {
        string query = Query.Trim();
        _filtered = _frames.Where(frame => query.Length == 0 || frame.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            frame.Passages.Any(passage => passage.Text.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        int keep = 0;
        while (keep < Visible.Count && keep < _filtered.Length && ReferenceEquals(Visible[keep], _filtered[keep])) keep++;
        while (Visible.Count > keep) Visible.RemoveAt(Visible.Count - 1);
        int target = Math.Min(_filtered.Length, Math.Max(PageSize, keep));
        for (int i = Visible.Count; i < target; i++) Visible.Add(_filtered[i]);
        CountText = string.Format(CultureInfo.CurrentCulture, _text.GetString("CaptureVideoText_TimestampCount"), _filtered.Length);
        RaisePropertyChanged(nameof(HasNoMatches));
        UpdatePaging();
    }

    private void LoadMore()
    {
        int target = Math.Min(_filtered.Length, Visible.Count + PageSize);
        for (int i = Visible.Count; i < target; i++) Visible.Add(_filtered[i]);
        UpdatePaging();
    }

    private void UpdatePaging()
    {
        RaisePropertyChanged(nameof(HasMore));
        LoadMoreCommand.NotifyCanExecuteChanged();
    }
}
