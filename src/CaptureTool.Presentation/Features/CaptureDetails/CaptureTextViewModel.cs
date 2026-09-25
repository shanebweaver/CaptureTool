using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed record CaptureTextFilter(string Label, CaptureTextSource? Source);

/// <summary>Search/copy scope is independent of the bounded set of visual rows.</summary>
public sealed class CaptureTextViewModel : ViewModelBase
{
    private const int PageSize = 100;
    private readonly ILocalizationService _text;
    private IReadOnlyList<CaptureTextPassage> _all = [];
    private CaptureTextPassage[] _filtered = [];
    private CaptureTextNavigationContext _context = new(false, false);
    public ObservableCollection<CaptureTextPassage> Visible { get; } = [];
    public ObservableCollection<CaptureTextFilter> Filters { get; }
    public string Query { get; set { if (Set(ref field, value)) Filter(); } } = string.Empty;
    public CaptureTextFilter? SelectedFilter { get; set { if (Set(ref field, value)) Filter(); } }
    public CaptureTextPassage? SelectedPassage { get; set { if (Set(ref field, value)) UpdateCount(); } }
    public bool HasMore => Visible.Count < _filtered.Length;
    public bool HasText => _filtered.Length > 0;
    public bool HasSources => _all.Select(passage => passage.Source).Distinct().Take(2).Count() > 1;
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;
    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand LoadMoreCommand { get; }
    public event Action<CaptureTextPassage>? ScrollRequested;

    public CaptureTextViewModel(ILocalizationService text)
    {
        _text = text;
        Filters = [new(Text("AllSources"), null), new(Text("ScreenText"), CaptureTextSource.ImageText),
            new(Text("Speech"), CaptureTextSource.Speech), new(Text("Codes"), CaptureTextSource.QrCode)];
        SelectedFilter = Filters[0];
        NextCommand = new RelayCommand(() => Move(1), () => HasText && SelectedIndex < _filtered.Length - 1);
        PreviousCommand = new RelayCommand(() => Move(-1), () => SelectedIndex > 0);
        LoadMoreCommand = new RelayCommand(LoadMore, () => HasMore);
    }

    public void Replace(IReadOnlyList<CaptureTextPassage> passages)
    {
        var old = _all.ToDictionary(passage => passage.Id, StringComparer.Ordinal);
        var replacement = passages.Select(passage => old.TryGetValue(passage.Id, out var prior) && prior.SameContent(passage) ? prior : passage).ToArray();
        if (_all.SequenceEqual(replacement)) return;
        _all = replacement;
        SetNavigationContext(_context);
        Filter();
        RaisePropertyChanged(nameof(HasSources));
    }

    public void SetNavigationContext(CaptureTextNavigationContext context)
    {
        _context = context;
        foreach (var passage in _all) passage.SetNavigationContext(context, Text("LocationUnavailable"));
    }

    public string CopyVisibleScope() => string.Join(Environment.NewLine, _filtered.Select(passage => passage.Text));

    private void Filter()
    {
        string query = Query.Trim();
        var filtered = _all.Where(passage => (SelectedFilter?.Source == null || passage.Source == SelectedFilter.Source) &&
            (query.Length == 0 || passage.Text.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach (var passage in filtered) passage.Query = query;
        if (_filtered.SequenceEqual(filtered)) { UpdateCount(); return; }
        _filtered = filtered;
        CaptureTextPassage? selected = SelectedPassage;
        // Keep containers (and their text selection/scroll offsets) when the visible prefix is unchanged.
        int keep = 0;
        while (keep < Visible.Count && keep < filtered.Length && ReferenceEquals(Visible[keep], filtered[keep])) keep++;
        while (Visible.Count > keep) Visible.RemoveAt(Visible.Count - 1);
        int target = Math.Min(filtered.Length, Math.Max(PageSize, keep));
        for (int i = Visible.Count; i < target; i++) Visible.Add(filtered[i]);
        SelectedPassage = selected != null && filtered.Contains(selected) ? selected : null;
        UpdateCount();
    }

    private void LoadMore()
    {
        int target = Math.Min(_filtered.Length, Visible.Count + PageSize);
        for (int i = Visible.Count; i < target; i++) Visible.Add(_filtered[i]);
        UpdateCount();
    }

    private void Move(int direction)
    {
        if (_filtered.Length == 0) return;
        int next = SelectedIndex + direction;
        if (next < 0 || next >= _filtered.Length) return;
        while (Visible.Count <= next) LoadMore();
        SelectedPassage = _filtered[next];
        ScrollRequested?.Invoke(SelectedPassage);
    }

    private int SelectedIndex => SelectedPassage == null ? -1 : Array.IndexOf(_filtered, SelectedPassage);

    private void UpdateCount()
    {
        CountText = string.Format(CultureInfo.CurrentCulture, Text(Query.Trim().Length == 0 ? "PassageCount" : "MatchCount"), _filtered.Length);
        RaisePropertyChanged(nameof(HasMore)); RaisePropertyChanged(nameof(HasText));
        NextCommand?.NotifyCanExecuteChanged(); PreviousCommand?.NotifyCanExecuteChanged(); LoadMoreCommand?.NotifyCanExecuteChanged();
    }
    private string Text(string key) => _text.GetString("CapturePane_" + key);
}
