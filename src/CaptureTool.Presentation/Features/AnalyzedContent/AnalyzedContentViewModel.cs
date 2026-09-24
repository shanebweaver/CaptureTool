using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.Features.CaptureMemory;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CaptureTool.Presentation.Features.AnalyzedContent;

public enum AnalyzedContentSectionKind { None, Transcript, ImageText, ImageDescription, VideoText, VideoDescription, Properties, All }

public sealed class AnalyzedContentItemViewModel : ViewModelBase
{
    internal AnalyzedContentItemViewModel(CaptureAnalyzedPassage passage, Action<AnalyzedContentItemViewModel> activate,
        ILocalizationService? localization)
    {
        Passage = passage;
        SourceLabel = CaptureMemoryEvidenceViewModel.SourceName(passage.MatchKind, localization);
        ActivateCommand = new RelayCommand(() => activate(this));
    }
    public CaptureAnalyzedPassage Passage { get; }
    public string EvidenceId => Passage.EvidenceId;
    public CaptureMemoryMatchKind MatchKind => Passage.MatchKind;
    public string Text => Passage.Text;
    public TimeSpan? StartTime => Passage.StartTime;
    public TimeSpan? EndTime => Passage.EndTime;
    public PixelRect? ImageBounds => Passage.PixelBounds is { } bounds ? new(bounds.X, bounds.Y, bounds.Width, bounds.Height) : null;
    public string SecondaryLabel => Passage.SecondaryLabel ?? string.Empty;
    public bool HasSecondaryLabel => SecondaryLabel.Length > 0;
    public string SourceLabel { get; }
    public string SourceGlyph => CaptureMemoryEvidenceViewModel.Glyph(MatchKind);
    public string TimecodeLabel => StartTime is TimeSpan time ? FormatTimecode(time) : string.Empty;
    public bool HasTimecode => StartTime.HasValue;
    public IRelayCommand ActivateCommand { get; }
    public bool IsActive { get; internal set => Set(ref field, value); }
    public bool IsSelected { get; internal set { if (Set(ref field, value)) { RaisePropertyChanged(nameof(IsHighlighted)); } } }
    public bool IsHighlighted => IsSelected;
    public bool IsSeekEnabled { get; internal set { if (Set(ref field, value)) { RaisePropertyChanged(nameof(CanActivate)); } } }
    public bool IsLocationCurrent { get; internal set { if (Set(ref field, value)) { RaisePropertyChanged(nameof(CanActivate)); } } } = true;
    public bool CanActivate => IsLocationCurrent && (ImageBounds.HasValue || IsSeekEnabled);
    public IReadOnlyList<CaptureTextRange> Highlights { get; internal set => Set(ref field, value); } = [];
    public string ContextText { get; internal set => Set(ref field, value); } = string.Empty;
    public string LocationMessage
    {
        get;
        internal set { if (Set(ref field, value)) { RaisePropertyChanged(nameof(AutomationName)); } }
    } = string.Empty;
    public string AutomationName => $"{SourceLabel} {TimecodeLabel}. {Text}. {LocationMessage}";
    internal static string FormatTimecode(TimeSpan time) => CaptureMemoryEvidenceViewModel.FormatTimecode(time);
}

public sealed class AnalyzedContentSectionViewModel : ViewModelBase
{
    internal AnalyzedContentSectionViewModel(AnalyzedContentSectionKind kind, string title, string fullText,
        IEnumerable<AnalyzedContentItemViewModel> items, string emptyMessage, Func<AnalyzedContentSectionViewModel, Task> copy)
    {
        Kind = kind; Title = title; FullText = fullText; EmptyMessage = emptyMessage;
        Items = new(new ObservableCollection<AnalyzedContentItemViewModel>(items));
        CopyAllCommand = new AsyncRelayCommand(() => copy(this), () => HasText, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }
    public AnalyzedContentSectionKind Kind { get; }
    public string Title { get; }
    public string FullText { get; }
    public string EmptyMessage { get; }
    public ReadOnlyObservableCollection<AnalyzedContentItemViewModel> Items { get; }
    public IAsyncRelayCommand CopyAllCommand { get; }
    public bool HasItems => Items.Count > 0;
    public bool HasText => !string.IsNullOrWhiteSpace(FullText);
    public bool HasResult => HasItems || HasText;
    public bool ShowFullText => !HasItems && HasText;
    public bool ShowEmpty => !HasResult;
}

public sealed partial class AnalyzedContentViewModel : ViewModelBase
{
    private readonly ICaptureMetadataViewService? _metadata;
    private readonly ICaptureAnalysisChangeNotifier? _changeNotifier;
    private readonly ICaptureAnalysisActivityQueryService? _activity;
    private readonly IClipboardService? _clipboard;
    private readonly ILocalizationService? _localization;
    private readonly INavigationCoordinator? _navigation;
    private readonly ObservableCollection<AnalyzedContentSectionViewModel> _sections = [];
    private readonly ObservableCollection<AnalyzedContentSectionViewModel> _sourceOptions = [];
    private readonly ObservableCollection<AnalyzedContentItemViewModel> _filteredItems = [];
    private readonly List<AnalyzedContentItemViewModel> _matches = [];
    private CancellationTokenSource? _refreshCancellation;
    private CaptureMetadataViewRequest? _request;
    private CaptureMemoryMatchEvidence? _initialMatch;
    private CaptureMemorySearchContext? _searchContext;
    private CaptureMetadataViewSnapshot? _snapshot;
    private SynchronizationContext? _uiContext;
    private int _refreshGeneration;
    private bool _disposed;
    private bool _applyingSnapshot;
    private TimeSpan? _minimumSeekTime;
    private TimeSpan? _maximumSeekTime;
    private AnalyzedContentItemViewModel? _selectedItem;
    private AnalyzedContentItemViewModel? _lastFollowedItem;
    private string _searchQuery = string.Empty;
    private AnalyzedContentSectionViewModel _selectedSection;

    public AnalyzedContentViewModel(ICaptureMetadataViewService? metadata = null,
        ICaptureAnalysisChangeNotifier? changeNotifier = null, IClipboardService? clipboard = null,
        ILocalizationService? localization = null, IAppNotificationService? notifications = null,
        INavigationCoordinator? navigation = null, ICaptureAnalysisActivityQueryService? activity = null)
    {
        _metadata = metadata; _changeNotifier = changeNotifier; _clipboard = clipboard;
        _localization = localization; _navigation = navigation; _activity = activity;
        _selectedSection = CreateAllSection();
        Sections = new(_sections); SourceOptions = new(_sourceOptions); FilteredItems = new(_filteredItems);
        TogglePaneCommand = new RelayCommand(() => IsPaneOpen = !IsPaneOpen);
        ClosePaneCommand = new RelayCommand(() => IsPaneOpen = false);
        ClearSearchCommand = new RelayCommand(() => SearchQuery = string.Empty);
        ShowMatchesCommand = new RelayCommand(() => IsMatchesView = true);
        ShowAllContentCommand = new RelayCommand(() => IsMatchesView = false);
        PreviousMatchCommand = new RelayCommand(() => MoveMatch(-1), () => MatchIndex > 0);
        NextMatchCommand = new RelayCommand(() => MoveMatch(1), () => _matches.Count > 0 && MatchIndex < _matches.Count - 1);
        CopyCommand = new AsyncRelayCommand(CopyVisibleAsync, () => HasSelectedItems, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RetryCommand = new AsyncRelayCommand(() => RefreshCompletion = RefreshAsync(), AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        BackToResultsCommand = new AsyncRelayCommand(async () =>
        {
            if (_navigation != null) { await _navigation.NavigateAsync(NavigationRoute.Home); }
        }, () => HasSearchContext && _navigation != null, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public event EventHandler<TimeSpan>? SeekRequested;
    public event EventHandler<PixelRect?>? ImageBoundsFocusRequested;
    public event EventHandler<bool>? ImageTextVisibilityRequested;
    public event EventHandler<CaptureMetadataViewSnapshot?>? MetadataChanged;
    public event EventHandler<AnalyzedContentItemViewModel>? ItemFocusRequested;
    public ReadOnlyObservableCollection<AnalyzedContentSectionViewModel> Sections { get; }
    public ReadOnlyObservableCollection<AnalyzedContentSectionViewModel> SourceOptions { get; }
    public ReadOnlyObservableCollection<AnalyzedContentItemViewModel> FilteredItems { get; }
    public Task RefreshCompletion { get; private set; } = Task.CompletedTask;
    public IRelayCommand TogglePaneCommand { get; }
    public IRelayCommand ClosePaneCommand { get; }
    public IRelayCommand ClearSearchCommand { get; }
    public IRelayCommand ShowMatchesCommand { get; }
    public IRelayCommand ShowAllContentCommand { get; }
    public IRelayCommand PreviousMatchCommand { get; }
    public IRelayCommand NextMatchCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand RetryCommand { get; }
    public IAsyncRelayCommand BackToResultsCommand { get; }
    public bool HasSearchContext => _searchContext != null;
    public bool HasContent => _sections.Any(section => section.HasResult);
    public bool IsSearchAvailable => _sections.Count > 0;
    public bool ShowSourceSelector => _sections.Count > 1;
    public bool HasSelectedItems => _filteredItems.Count > 0;
    public bool ShowSelectedFullText => false;
    public bool ShowSelectedEmpty => !IsMetadataLoading && !HasSelectedItems;
    public bool IsAllContentView => !IsMatchesView;
    public bool HasQuery => !string.IsNullOrWhiteSpace(SearchQuery);
    public int MatchCount => _matches.Count;
    private int MatchIndex => _selectedItem == null ? -1 : _matches.IndexOf(_selectedItem);
    public string MatchCountLabel => HasQuery
        ? MatchIndex >= 0 ? Format("Analysis_MatchPosition", "Match {0} of {1}", MatchIndex + 1, MatchCount)
            : Format("Analysis_MatchCount", "Matches: {0}", MatchCount)
        : Format("Analysis_PassageCount", "Passages: {0}", _filteredItems.Count);
    public bool IsMetadataLoading { get; private set => Set(ref field, value); }
    public bool HasLoadFailure { get; private set => Set(ref field, value); }
    public bool HasPendingAnalysis { get; private set => Set(ref field, value); }
    public string StatusText { get; private set => Set(ref field, value); } = string.Empty;
    private string _lastSuccessfulStatusText = string.Empty;
    public string NavigationMessage { get; private set => Set(ref field, value); } = string.Empty;
    public string CopyStatusText { get; private set => Set(ref field, value); } = string.Empty;
    public string DetailsText { get; private set => Set(ref field, value); } = string.Empty;
    public bool HasDetails => DetailsText.Length > 0;
    public bool FollowPlayback { get; set => Set(ref field, value); }
    public bool HasTimedContent => _sections.SelectMany(section => section.Items).Any(item => item.HasTimecode);
    public AnalyzedContentItemViewModel? SelectedItem => _selectedItem;
    public string CopyActionLabel => HasQuery && IsMatchesView ? GetString("Analysis_CopyMatches", "Copy matches") :
        (SelectedSection.Kind == AnalyzedContentSectionKind.All && _sections.Count == 1 ? _sections[0].Kind : SelectedSection.Kind) switch
        {
            AnalyzedContentSectionKind.Transcript => GetString("Analysis_CopyTranscript", "Copy transcript"),
            AnalyzedContentSectionKind.ImageText or AnalyzedContentSectionKind.VideoText => GetString("Analysis_CopyText", "Copy text"),
            AnalyzedContentSectionKind.ImageDescription or AnalyzedContentSectionKind.VideoDescription => GetString("Analysis_CopyDescription", "Copy description"),
            _ => GetString("Analysis_CopyAll", "Copy analysis text"),
        };
    public string SelectedEmptyMessage => HasLoadFailure ? GetString("Analysis_LoadFailed", "Analysis could not be loaded. Try again.") :
        HasQuery && IsMatchesView && HasContent ? GetString("AnalyzedContent_NoSearchResults", "No matching results.") :
        SelectedSection.Kind != AnalyzedContentSectionKind.All ? SelectedSection.EmptyMessage : StatusText;
    public string EmptyMessage => GetString("AnalyzedContent_NoContentMessage", "No analyzed content is available for this capture.");

    public bool IsPaneOpen
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                RequestImageTextVisibility();
                if (value && _selectedItem != null) { ItemFocusRequested?.Invoke(this, _selectedItem); }
            }
        }
    }
    public bool IsMatchesView
    {
        get;
        set { if (Set(ref field, value)) { RefreshFilteredItems(); RaisePropertyChanged(nameof(IsAllContentView)); } }
    }
    public AnalyzedContentSectionViewModel SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (value != null && Set(ref _selectedSection, value) && !_applyingSnapshot)
            {
                RefreshFilteredItems(); RequestImageTextVisibility();
            }
        }
    }
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            value ??= string.Empty;
            if (value.Length > CaptureMemorySearchRequest.MaximumQueryLength) { value = value[..CaptureMemorySearchRequest.MaximumQueryLength]; }
            bool hadQuery = HasQuery;
            if (Set(ref _searchQuery, value))
            {
                if (!HasQuery || !hadQuery) { IsMatchesView = HasQuery; }
                RefreshFilteredItems();
            }
        }
    }

    public void Load(CaptureMetadataViewRequest request, CaptureMemoryMatchEvidence? initialMatch = null,
        CaptureMemorySearchContext? searchContext = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        _disposed = false; _request = request; _initialMatch = initialMatch; _searchContext = searchContext;
        NavigationMessage = string.Empty;
        _uiContext = SynchronizationContext.Current;
        SearchQuery = searchContext?.Query ?? string.Empty;
        FollowPlayback = searchContext == null;
        if (_changeNotifier != null) { _changeNotifier.AnalysisChanged -= AnalysisChanged; _changeNotifier.AnalysisChanged += AnalysisChanged; }
        if (_activity != null) { _activity.ActivityChanged -= ActivityChanged; _activity.ActivityChanged += ActivityChanged; }
        if (initialMatch is { MatchKind: not CaptureMemoryMatchKind.Filename }) { IsPaneOpen = true; }
        RaisePropertyChanged(nameof(HasSearchContext)); BackToResultsCommand.NotifyCanExecuteChanged();
        RefreshCompletion = RefreshAsync();
    }

    public void UpdatePlaybackPosition(TimeSpan position)
    {
        foreach (var section in _sections)
        {
            var timed = section.Items.Where(item => item.StartTime.HasValue).OrderBy(item => item.StartTime).ToArray();
            for (int index = 0; index < timed.Length; index++)
            {
                var item = timed[index];
                TimeSpan? end = item.EndTime ?? (index + 1 < timed.Length ? timed[index + 1].StartTime : _maximumSeekTime);
                item.IsActive = position >= item.StartTime && (!end.HasValue || position < end);
            }
        }
        var active = _filteredItems.FirstOrDefault(item => item.IsActive);
        if (FollowPlayback && IsPaneOpen && active != null && active != _lastFollowedItem)
        {
            _lastFollowedItem = active; ItemFocusRequested?.Invoke(this, active);
        }
    }

    private bool _isImageLocationAvailable = true;
    public void SetImageLocationAvailability(bool available)
    {
        _isImageLocationAvailable = available;
        UpdateSeekAvailability();
        RequestImageTextVisibility();
    }

    public void SetSeekRange(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumSeekTime = minimum; _maximumSeekTime = maximum; UpdateSeekAvailability();
    }

    public override void Dispose()
    {
        _disposed = true; _refreshGeneration++;
        if (_changeNotifier != null) { _changeNotifier.AnalysisChanged -= AnalysisChanged; }
        if (_activity != null) { _activity.ActivityChanged -= ActivityChanged; }
        _refreshCancellation?.Cancel(); _refreshCancellation?.Dispose(); _refreshCancellation = null;
        base.Dispose();
    }

    private async Task RefreshAsync()
    {
        if (_metadata == null || _request == null || _disposed) { return; }
        int generation = Interlocked.Increment(ref _refreshGeneration);
        CancellationTokenSource cancellation = new();
        var previous = Interlocked.Exchange(ref _refreshCancellation, cancellation);
        previous?.Cancel(); previous?.Dispose();
        Post(() => { IsMetadataLoading = _snapshot == null; HasLoadFailure = false; NotifyDisplay(); });
        try
        {
            var snapshot = await _metadata.GetAsync(_request, cancellation.Token).ConfigureAwait(false);
            Post(() => { if (generation == _refreshGeneration && !_disposed) { ApplySnapshot(snapshot); } });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch
        {
            Post(() =>
            {
                if (generation != _refreshGeneration || _disposed) { return; }
                IsMetadataLoading = false; HasLoadFailure = true;
                StatusText = GetString("Analysis_LoadFailed", "Analysis could not be loaded. Try again."); NotifyDisplay();
            });
        }
    }

    private void ApplySnapshot(CaptureMetadataViewSnapshot? snapshot)
    {
        if (_initialMatch == null && snapshot?.Passages != null && _snapshot?.Passages != null &&
            snapshot.CaptureId == _snapshot.CaptureId && snapshot.DocumentRevision == _snapshot.DocumentRevision &&
            snapshot.SourceRevision == _snapshot.SourceRevision && snapshot.IsLocationCurrent == _snapshot.IsLocationCurrent &&
            snapshot.IsEnrolled == _snapshot.IsEnrolled && snapshot.IsExcluded == _snapshot.IsExcluded &&
            snapshot.CapabilityStates.SequenceEqual(_snapshot.CapabilityStates) && snapshot.Passages.SequenceEqual(_snapshot.Passages))
        {
            // Activity polling must not reset selection, text selection or the reader's scroll position.
            IsMetadataLoading = false;
            StatusText = _lastSuccessfulStatusText;
            NotifyDisplay();
            return;
        }
        string? selectedId = _selectedItem?.EvidenceId;
        AnalyzedContentSectionKind selectedKind = SelectedSection.Kind;
        _snapshot = snapshot; _applyingSnapshot = true; _sections.Clear(); _sourceOptions.Clear(); _selectedItem = null;
        if (snapshot != null)
        {
            foreach (var definition in Definitions(snapshot))
            {
                var passages = snapshot.Passages?.Where(passage => passage.MatchKind == definition.Kind).ToArray() ?? definition.LegacyPassages.ToArray();
                var state = snapshot.CapabilityStates.FirstOrDefault(state => state.MatchKind == definition.Kind)?.State;
                if (passages.Length == 0 && state == null && string.IsNullOrWhiteSpace(definition.FullText)) { continue; }
                var items = passages.Select(passage => new AnalyzedContentItemViewModel(passage, ActivateItem, _localization)).ToArray();
                string name = CaptureMemoryEvidenceViewModel.SourceName(definition.Kind, _localization);
                string status = ProcessingText(state ?? CaptureMetadataProcessingState.Ready, definition.Kind);
                _sections.Add(new(ToSectionKind(definition.Kind), name, definition.FullText, items, status, CopySectionAsync));
            }
        }
        _sourceOptions.Add(CreateAllSection());
        foreach (var section in _sections) { _sourceOptions.Add(section); }
        SelectedSection = _sourceOptions.FirstOrDefault(section => section.Kind == selectedKind) ?? _sourceOptions[0];
        _applyingSnapshot = false;
        _selectedItem = _sections.SelectMany(section => section.Items).FirstOrDefault(item => item.EvidenceId == selectedId);
        if (_selectedItem != null) { _selectedItem.IsSelected = true; }
        DetailsText = DescribeProperties(snapshot?.MediaProperties);
        IsMetadataLoading = false; HasLoadFailure = false;
        HasPendingAnalysis = snapshot?.CapabilityStates.Any(state => state.State is CaptureMetadataProcessingState.Queued or
            CaptureMetadataProcessingState.Analyzing or CaptureMetadataProcessingState.WaitingForModel) == true;
        StatusText = snapshot switch
        {
            { IsExcluded: true } => GetString("Analysis_Excluded", "This capture was removed from Memory."),
            { IsEnrolled: false } => GetString("Analysis_NotEnrolled", "This capture is not included in Capture Memory."),
            { IsLocationCurrent: false } => GetString("Analysis_SourceChanged", "This media differs from the analyzed source. Passage and region navigation is unavailable."),
            _ when HasPendingAnalysis => GetString("Analysis_Processing", "Analysis is in progress. Available content can be searched now."),
            _ when snapshot?.CapabilityStates.Any(state => state.State == CaptureMetadataProcessingState.Failed) == true =>
                GetString("Analysis_PartialFailure", "Some analysis failed. Available content is still searchable."),
            _ when snapshot?.CapabilityStates.Any(state => state.State == CaptureMetadataProcessingState.Unsupported) == true =>
                GetString("Analysis_PartlyAvailable", "Some analysis is unavailable on this device."),
            _ when HasContent => GetString("Analysis_Ready", "Analysis ready · generated on this device"),
            _ when _sections.Count == 1 => _sections[0].EmptyMessage,
            _ => EmptyMessage,
        };
        _lastSuccessfulStatusText = StatusText;
        MetadataChanged?.Invoke(this, snapshot);
        UpdateSeekAvailability(); RefreshFilteredItems(); ApplyInitialMatch(); RequestImageTextVisibility(); NotifyDisplay();
    }

    private void RefreshFilteredItems()
    {
        if (_applyingSnapshot) { return; }
        _filteredItems.Clear(); _matches.Clear();
        var sections = SelectedSection.Kind == AnalyzedContentSectionKind.All ? _sections.ToArray() : new[] { SelectedSection };
        var visible = new List<AnalyzedContentItemViewModel>();
        foreach (var section in sections)
        {
            var sectionMatches = new List<AnalyzedContentItemViewModel>();
            foreach (var item in section.Items)
            {
                CaptureTextMatch match = CaptureMemoryTextNormalizer.FindMatches(item.Text, SearchQuery, includePartialHighlights: true);
                item.Highlights = match.Ranges; item.ContextText = string.Empty;
                if (HasQuery && match.IsMatch) { sectionMatches.Add(item); }
                if (!IsMatchesView || !HasQuery || match.IsMatch) { visible.Add(item); }
            }
            if (HasQuery && sectionMatches.Count == 0 && CaptureMemoryTextNormalizer.FindMatches(section.FullText, SearchQuery).IsMatch)
            {
                CaptureMemoryMatchKind kind = ToMatchKind(section.Kind);
                string prefix = section.Items.FirstOrDefault()?.EvidenceId.Split(':')[0] ?? ((int)kind).ToString(CultureInfo.InvariantCulture);
                var combined = new AnalyzedContentItemViewModel(new(kind, prefix + ":-1", section.FullText,
                    IsCombinedMatch: true), ActivateItem, _localization)
                {
                    Highlights = CaptureMemoryTextNormalizer.FindMatches(section.FullText, SearchQuery).Ranges,
                    LocationMessage = GetString("Analysis_MatchAcrossPassages", "Across multiple passages"),
                };
                sectionMatches.Add(combined);
                if (IsMatchesView) { visible.Add(combined); }
            }
            _matches.AddRange(sectionMatches);
        }
        foreach (var item in visible.OrderBy(item => item.StartTime ?? TimeSpan.MaxValue).ThenBy(item => item.MatchKind)) { _filteredItems.Add(item); }
        var orderedMatches = _matches.OrderBy(item => item.StartTime ?? TimeSpan.MaxValue).ThenBy(item => item.MatchKind).ToArray();
        _matches.Clear(); _matches.AddRange(orderedMatches);
        if (_selectedItem != null)
        {
            _selectedItem = _filteredItems.FirstOrDefault(item => item.EvidenceId == _selectedItem.EvidenceId) ?? _selectedItem;
            _selectedItem.IsSelected = true; AddContext(_selectedItem);
        }
        CopyStatusText = string.Empty; NotifyDisplay();
    }

    private void ActivateItem(AnalyzedContentItemViewModel item)
    {
        foreach (var candidate in _sections.SelectMany(section => section.Items).Concat(_filteredItems).Distinct())
        {
            candidate.IsSelected = ReferenceEquals(candidate, item); candidate.ContextText = string.Empty;
        }
        _selectedItem = item; item.IsSelected = true; FollowPlayback = false; AddContext(item);
        RequestImageTextVisibility(); ItemFocusRequested?.Invoke(this, item);
        if (item.CanActivate)
        {
            if (item.ImageBounds is PixelRect bounds) { ImageBoundsFocusRequested?.Invoke(this, bounds); }
            if (item.StartTime is TimeSpan start && item.IsSeekEnabled) { SeekRequested?.Invoke(this, start); }
        }
        NotifyDisplay();
    }

    private void ApplyInitialMatch()
    {
        if (_initialMatch is not { } match || match.MatchKind == CaptureMemoryMatchKind.Filename) { _initialMatch = null; return; }
        if (_snapshot == null) { return; }
        var items = _sections.SelectMany(section => section.Items).Concat(_matches).Distinct().ToArray();
        AnalyzedContentItemViewModel? item = match.EvidenceId != null
            ? items.FirstOrDefault(candidate => candidate.EvidenceId == match.EvidenceId)
            : items.FirstOrDefault(candidate => candidate.MatchKind == match.MatchKind &&
                (match.Timecode.HasValue ? candidate.StartTime == match.Timecode :
                    candidate.Text.Contains(match.Snippet, StringComparison.CurrentCultureIgnoreCase) ||
                    match.Snippet.Contains(candidate.Text, StringComparison.CurrentCultureIgnoreCase)));
        if (_searchContext?.SourceRevision is { } expected && _snapshot.SourceRevision is { } actual && !expected.HasSameBytesAs(actual))
        {
            NavigationMessage = GetString("Analysis_MatchChanged", "Analysis changed since this search. Current matches are shown below.");
            _initialMatch = null; return;
        }
        if (item != null)
        {
            if (_searchContext == null) { SelectedSection = _sections.First(section => section.Kind == ToSectionKind(match.MatchKind)); }
            IsPaneOpen = true; ActivateItem(item); _initialMatch = null;
        }
        else if (!HasPendingAnalysis)
        {
            NavigationMessage = GetString("Analysis_MatchChanged", "Analysis changed since this search. Current matches are shown below.");
            _initialMatch = null;
        }
    }

    private void AddContext(AnalyzedContentItemViewModel item)
    {
        var section = _sections.FirstOrDefault(section => section.Items.Contains(item));
        if (section == null || !item.StartTime.HasValue) { return; }
        int index = section.Items.IndexOf(item);
        string context = string.Join(" ", new[] { index > 0 ? section.Items[index - 1].Text : string.Empty,
            index + 1 < section.Items.Count ? section.Items[index + 1].Text : string.Empty }.Where(text => text.Length > 0));
        item.ContextText = context.Length <= 320 ? context : context[..319] + "…";
    }

    private void MoveMatch(int direction)
    {
        if (_matches.Count == 0) { return; }
        int index = Math.Clamp(MatchIndex < 0 ? 0 : MatchIndex + direction, 0, _matches.Count - 1);
        if (!IsMatchesView && !_filteredItems.Contains(_matches[index])) { IsMatchesView = true; }
        ActivateItem(_matches[index]);
    }

    private void UpdateSeekAvailability()
    {
        foreach (var item in _sections.SelectMany(section => section.Items))
        {
            item.IsLocationCurrent = _snapshot?.IsLocationCurrent != false && _isImageLocationAvailable;
            item.IsSeekEnabled = item.StartTime is TimeSpan start &&
                (!_minimumSeekTime.HasValue || start >= _minimumSeekTime.Value) &&
                (!_maximumSeekTime.HasValue || start < _maximumSeekTime.Value);
            item.LocationMessage = !_isImageLocationAvailable ? GetString("Analysis_EditedLocation", "Region navigation is unavailable for the current edits") :
                !item.IsLocationCurrent ? GetString("Analysis_LocationUnavailable", "Location unavailable for this media") :
                item.HasTimecode && !item.IsSeekEnabled ? GetString("Analysis_OutsideTrim", "Outside the current playback range") :
                item.Passage.IsCombinedMatch ? GetString("Analysis_MatchAcrossPassages", "Across multiple passages") : string.Empty;
        }
    }

    private void RequestImageTextVisibility() => ImageTextVisibilityRequested?.Invoke(this,
        IsPaneOpen && _isImageLocationAvailable && _snapshot?.IsLocationCurrent != false && (SelectedSection.Kind == AnalyzedContentSectionKind.ImageText ||
            SelectedSection.Kind == AnalyzedContentSectionKind.All && _selectedItem?.MatchKind == CaptureMemoryMatchKind.OcrText));
    private void AnalysisChanged(object? sender, CaptureAnalysisChangedEventArgs args)
    {
        if (_request?.CaptureId == null || _request.CaptureId == args.CaptureId) { RefreshCompletion = RefreshAsync(); }
    }
    private void ActivityChanged(object? sender, EventArgs args)
    {
        if (IsPaneOpen || HasPendingAnalysis) { RefreshCompletion = RefreshAsync(); }
    }
    private void Post(Action action)
    {
        if (_uiContext == null || ReferenceEquals(SynchronizationContext.Current, _uiContext)) { action(); }
        else { _uiContext.Post(_ => action(), null); }
    }
    private Task CopySectionAsync(AnalyzedContentSectionViewModel section) => CopyTextAsync(section.FullText);
    private Task CopyVisibleAsync() => CopyTextAsync(string.Join(Environment.NewLine, _filteredItems.Select(item =>
        item.HasTimecode ? $"{item.TimecodeLabel} {item.Text}" : item.Text)));
    private async Task CopyTextAsync(string text)
    {
        if (_clipboard == null || string.IsNullOrWhiteSpace(text)) { return; }
        try { await _clipboard.CopyTextAsync(text); CopyStatusText = GetString("Analysis_Copied", "Copied"); }
        catch { CopyStatusText = GetString("AnalyzedContent_CopyFailed", "Analyzed content could not be copied."); }
    }
    private void NotifyDisplay()
    {
        foreach (string name in new[] { nameof(HasContent), nameof(IsSearchAvailable), nameof(ShowSourceSelector), nameof(HasSelectedItems),
            nameof(ShowSelectedFullText), nameof(ShowSelectedEmpty), nameof(SelectedEmptyMessage), nameof(MatchCount), nameof(MatchCountLabel),
            nameof(CopyActionLabel), nameof(HasDetails), nameof(HasTimedContent), nameof(HasQuery), nameof(SelectedItem) }) { RaisePropertyChanged(name); }
        PreviousMatchCommand.NotifyCanExecuteChanged(); NextMatchCommand.NotifyCanExecuteChanged(); CopyCommand.NotifyCanExecuteChanged();
    }
    private string GetString(string key, string fallback) => CaptureMemoryEvidenceViewModel.GetString(_localization, key, fallback);
    private string Format(string key, string fallback, params object[] values) => string.Format(CultureInfo.CurrentCulture, GetString(key, fallback), values);
    private AnalyzedContentSectionViewModel CreateAllSection() => new(AnalyzedContentSectionKind.All,
        GetString("Analysis_AllSources", "All sources"), string.Empty, [], string.Empty, CopySectionAsync);
    private string ProcessingText(CaptureMetadataProcessingState state, CaptureMemoryMatchKind kind) => state switch
    {
        CaptureMetadataProcessingState.Queued => GetString("Analysis_Queued", "Queued for analysis."),
        CaptureMetadataProcessingState.Analyzing => GetString("Analysis_Analyzing", "Analyzing this capture…"),
        CaptureMetadataProcessingState.WaitingForModel => GetString("Analysis_Waiting", "Waiting for a model to become available."),
        CaptureMetadataProcessingState.Unsupported => GetString("Analysis_Unsupported", "This analysis is unavailable on this device."),
        CaptureMetadataProcessingState.Failed => GetString("Analysis_Failed", "This analysis failed. You can analyze again in Capture Memory settings."),
        CaptureMetadataProcessingState.NotAnalyzed => GetString("Analysis_NotAnalyzed", "This content has not been analyzed."),
        _ => kind == CaptureMemoryMatchKind.SpeechTranscript ? GetString("AnalyzedContent_NoSpeech", "No speech was detected.") :
            kind is CaptureMemoryMatchKind.OcrText or CaptureMemoryMatchKind.VideoOcrText ? GetString("AnalyzedContent_NoText", "No text was detected.") :
                GetString("AnalyzedContent_NoDescription", "No description was generated."),
    };
    private static AnalyzedContentSectionKind ToSectionKind(CaptureMemoryMatchKind kind) => kind switch
    {
        CaptureMemoryMatchKind.OcrText => AnalyzedContentSectionKind.ImageText,
        CaptureMemoryMatchKind.VideoOcrText => AnalyzedContentSectionKind.VideoText,
        CaptureMemoryMatchKind.SpeechTranscript => AnalyzedContentSectionKind.Transcript,
        CaptureMemoryMatchKind.ImageDescription => AnalyzedContentSectionKind.ImageDescription,
        CaptureMemoryMatchKind.VideoDescription => AnalyzedContentSectionKind.VideoDescription,
        _ => AnalyzedContentSectionKind.None,
    };
    private static CaptureMemoryMatchKind ToMatchKind(AnalyzedContentSectionKind kind) => kind switch
    {
        AnalyzedContentSectionKind.ImageText => CaptureMemoryMatchKind.OcrText,
        AnalyzedContentSectionKind.VideoText => CaptureMemoryMatchKind.VideoOcrText,
        AnalyzedContentSectionKind.Transcript => CaptureMemoryMatchKind.SpeechTranscript,
        AnalyzedContentSectionKind.ImageDescription => CaptureMemoryMatchKind.ImageDescription,
        AnalyzedContentSectionKind.VideoDescription => CaptureMemoryMatchKind.VideoDescription,
        _ => CaptureMemoryMatchKind.Filename,
    };
}
