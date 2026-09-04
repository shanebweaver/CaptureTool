using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CaptureTool.Presentation.Features.AnalyzedContent;

public enum AnalyzedContentSectionKind
{
    None,
    Transcript,
    ImageText,
    ImageDescription,
    VideoText,
    VideoDescription,
    Properties,
}

public sealed class AnalyzedContentItemViewModel : ViewModelBase
{
    private readonly Action<AnalyzedContentItemViewModel> _activate;

    internal AnalyzedContentItemViewModel(
        string text,
        Action<AnalyzedContentItemViewModel> activate,
        TimeSpan? startTime = null,
        TimeSpan? endTime = null,
        string? secondaryLabel = null,
        PixelRect? imageBounds = null)
    {
        Text = text;
        StartTime = startTime;
        EndTime = endTime;
        SecondaryLabel = secondaryLabel ?? string.Empty;
        ImageBounds = imageBounds;
        _activate = activate;
        ActivateCommand = new RelayCommand(Activate);
    }

    public IRelayCommand ActivateCommand { get; }

    public string Text { get; }

    public TimeSpan? StartTime { get; }

    public TimeSpan? EndTime { get; }

    public string SecondaryLabel { get; }

    public PixelRect? ImageBounds { get; }

    public bool HasSecondaryLabel => !string.IsNullOrWhiteSpace(SecondaryLabel);

    public bool HasTimecode => StartTime.HasValue;

    public string TimecodeLabel => StartTime.HasValue ? FormatTimecode(StartTime.Value) : string.Empty;

    public bool IsActive
    {
        get;
        internal set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(IsHighlighted));
            }
        }
    }

    public bool IsSelected
    {
        get;
        internal set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(IsHighlighted));
            }
        }
    }

    public bool IsSeekEnabled
    {
        get;
        internal set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanActivate));
            }
        }
    }

    public bool CanActivate => ImageBounds.HasValue || IsSeekEnabled;

    public bool IsHighlighted => IsActive || IsSelected;

    private void Activate() => _activate(this);

    internal static string FormatTimecode(TimeSpan value)
    {
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}

public sealed class AnalyzedContentSectionViewModel : ViewModelBase
{
    private readonly Func<AnalyzedContentSectionViewModel, Task> _copy;

    internal AnalyzedContentSectionViewModel(
        AnalyzedContentSectionKind kind,
        string title,
        string fullText,
        IEnumerable<AnalyzedContentItemViewModel> items,
        string emptyMessage,
        Func<AnalyzedContentSectionViewModel, Task> copy)
    {
        Kind = kind;
        Title = title;
        FullText = fullText;
        EmptyMessage = emptyMessage;
        Items = new ReadOnlyObservableCollection<AnalyzedContentItemViewModel>(
            new ObservableCollection<AnalyzedContentItemViewModel>(items));
        _copy = copy;
        CopyAllCommand = new AsyncRelayCommand(
            () => _copy(this),
            () => HasText,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
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

    public bool ShowEmpty => !HasItems && !HasText;
}

public sealed class AnalyzedContentViewModel : ViewModelBase
{
    private readonly ICaptureMetadataViewService? _metadata;
    private readonly ICaptureAnalysisChangeNotifier? _changeNotifier;
    private readonly IClipboardService? _clipboard;
    private readonly ILocalizationService? _localization;
    private readonly IAppNotificationService? _notifications;
    private readonly ICaptureAnalysisMaintenanceService? _maintenance;
    private readonly ObservableCollection<AnalyzedContentSectionViewModel> _sections = [];
    private CancellationTokenSource? _refreshCancellation;
    private CaptureMetadataViewRequest? _request;
    private CaptureMemoryMatchEvidence? _initialMatch;
    private SynchronizationContext? _synchronizationContext;
    private CaptureId? _resolvedCaptureId;
    private int _refreshGeneration;
    private TimeSpan? _minimumSeekTime;
    private TimeSpan? _maximumSeekTime;

    public AnalyzedContentViewModel(
        ICaptureMetadataViewService? metadata = null,
        ICaptureAnalysisChangeNotifier? changeNotifier = null,
        IClipboardService? clipboard = null,
        ILocalizationService? localization = null,
        IAppNotificationService? notifications = null,
        ICaptureAnalysisMaintenanceService? maintenance = null)
    {
        _metadata = metadata;
        _changeNotifier = changeNotifier;
        _clipboard = clipboard;
        _localization = localization;
        _notifications = notifications;
        _maintenance = maintenance;
        Sections = new ReadOnlyObservableCollection<AnalyzedContentSectionViewModel>(_sections);
        TogglePaneCommand = new RelayCommand(() => IsPaneOpen = !IsPaneOpen);
        ClosePaneCommand = new RelayCommand(() => IsPaneOpen = false);
        ReanalyzeAllCommand = new AsyncRelayCommand(
            () => ReanalyzeAsync(selectedSectionOnly: false),
            CanReanalyzeAll,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ReanalyzeSelectedCommand = new AsyncRelayCommand(
            () => ReanalyzeAsync(selectedSectionOnly: true),
            CanReanalyzeSelected,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        SelectedSection = CreateEmptySection();
    }

    public event EventHandler<TimeSpan>? SeekRequested;

    public event EventHandler<PixelRect?>? ImageBoundsFocusRequested;

    public event EventHandler<bool>? ImageTextVisibilityRequested;

    public event EventHandler<CaptureMetadataViewSnapshot?>? MetadataChanged;

    public ReadOnlyObservableCollection<AnalyzedContentSectionViewModel> Sections { get; }

    public Task RefreshCompletion { get; private set; } = Task.CompletedTask;

    public IRelayCommand TogglePaneCommand { get; }

    public IRelayCommand ClosePaneCommand { get; }

    public IAsyncRelayCommand ReanalyzeAllCommand { get; }

    public IAsyncRelayCommand ReanalyzeSelectedCommand { get; }

    public bool IsPaneOpen
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                RequestImageTextVisibility();
            }
        }
    }

    public bool HasContent
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsReanalyzing
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                ReanalyzeAllCommand.NotifyCanExecuteChanged();
                ReanalyzeSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public AnalyzedContentSectionViewModel SelectedSection
    {
        get;
        set
        {
            if (value != null && Set(ref field, value))
            {
                RaisePropertyChanged(nameof(HasSelectedItems));
                RaisePropertyChanged(nameof(ShowSelectedFullText));
                RaisePropertyChanged(nameof(ShowSelectedEmpty));
                ReanalyzeSelectedCommand.NotifyCanExecuteChanged();
                RequestImageTextVisibility();
            }
        }
    }

    public bool HasSelectedItems => SelectedSection.HasItems;

    public bool ShowSelectedFullText => SelectedSection.ShowFullText;

    public bool ShowSelectedEmpty => SelectedSection.ShowEmpty;

    public string EmptyMessage => GetString(
        "AnalyzedContent_NoContentMessage",
        "No analyzed content is available for this capture.");

    public void Load(
        CaptureMetadataViewRequest request,
        CaptureMemoryMatchEvidence? initialMatch = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        _request = request;
        _initialMatch = initialMatch;
        _synchronizationContext = SynchronizationContext.Current;
        if (_changeNotifier != null)
        {
            _changeNotifier.AnalysisChanged -= ChangeNotifier_AnalysisChanged;
            _changeNotifier.AnalysisChanged += ChangeNotifier_AnalysisChanged;
        }

        if (initialMatch is { MatchKind: not CaptureMemoryMatchKind.Filename })
        {
            IsPaneOpen = true;
        }

        ReanalyzeAllCommand.NotifyCanExecuteChanged();
        ReanalyzeSelectedCommand.NotifyCanExecuteChanged();

        RefreshCompletion = RefreshAsync();
    }

    public void UpdatePlaybackPosition(TimeSpan position)
    {
        foreach (AnalyzedContentItemViewModel item in _sections.SelectMany(section => section.Items))
        {
            item.IsActive = item.StartTime.HasValue &&
                position >= item.StartTime.Value &&
                (!item.EndTime.HasValue || position < item.EndTime.Value);
        }
    }

    public void SetSeekRange(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumSeekTime = minimum;
        _maximumSeekTime = maximum;
        UpdateSeekAvailability();
    }

    public override void Dispose()
    {
        if (_changeNotifier != null)
        {
            _changeNotifier.AnalysisChanged -= ChangeNotifier_AnalysisChanged;
        }

        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = null;
        base.Dispose();
    }

    private async Task RefreshAsync()
    {
        CaptureMetadataViewRequest? request = _request;
        if (_metadata == null || request == null)
        {
            return;
        }

        int generation = Interlocked.Increment(ref _refreshGeneration);
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous = Interlocked.Exchange(
            ref _refreshCancellation,
            cancellation);
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            CaptureMetadataViewSnapshot? snapshot = await _metadata
                .GetAsync(request, cancellation.Token)
                .ConfigureAwait(false);
            if (generation != _refreshGeneration || cancellation.IsCancellationRequested)
            {
                return;
            }

            PostToCapturedContext(() => ApplySnapshot(snapshot));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch
        {
            PostToCapturedContext(() => ApplySnapshot(null));
        }
    }

    private void ApplySnapshot(CaptureMetadataViewSnapshot? snapshot)
    {
        AnalyzedContentSectionKind selectedKind = SelectedSection.Kind;
        _resolvedCaptureId = snapshot?.CaptureId;
        ReanalyzeAllCommand.NotifyCanExecuteChanged();
        ReanalyzeSelectedCommand.NotifyCanExecuteChanged();
        _sections.Clear();
        if (snapshot != null)
        {
            AddSections(snapshot);
        }

        HasContent = _sections.Count > 0;
        SelectedSection = _sections.FirstOrDefault(section => section.Kind == selectedKind) ??
            _sections.FirstOrDefault() ??
            CreateEmptySection();
        MetadataChanged?.Invoke(this, snapshot);
        ApplyInitialMatch();
        UpdateSeekAvailability();
    }

    private void AddSections(CaptureMetadataViewSnapshot snapshot)
    {
        if (snapshot.SpeechTranscript is SpeechTranscriptV1 transcript)
        {
            AddSectionIfResult(CreateTranscriptSection(transcript));
        }

        if (snapshot.ImageText is OcrDocumentV1 imageText)
        {
            AddSectionIfResult(CreateImageTextSection(imageText));
        }

        if (snapshot.ImageDescription is ImageDescriptionV1 imageDescription)
        {
            AddSectionIfResult(CreateTextSection(
                AnalyzedContentSectionKind.ImageDescription,
                GetString("AnalyzedContent_ImageDescription", "Image description"),
                imageDescription.Description,
                GetString("AnalyzedContent_NoDescription", "No description was generated.")));
        }

        if (snapshot.VideoText is VideoOcrTrackV1 videoText)
        {
            AddSectionIfResult(CreateVideoTextSection(videoText));
        }

        if (snapshot.VideoDescription is VideoDescriptionTrackV1 videoDescription)
        {
            AddSectionIfResult(CreateVideoDescriptionSection(videoDescription));
        }

        if (snapshot.MediaProperties is MediaPropertiesV1 properties)
        {
            AddSectionIfResult(CreatePropertiesSection(properties));
        }
    }

    private AnalyzedContentSectionViewModel CreateTranscriptSection(SpeechTranscriptV1 transcript)
    {
        string language = string.IsNullOrWhiteSpace(transcript.LanguageTag)
            ? string.Empty
            : transcript.LanguageTag;
        AnalyzedContentItemViewModel[] items = transcript.Segments
            .Select(segment => new AnalyzedContentItemViewModel(
                segment.Text,
                ActivateItem,
                segment.StartTime,
                segment.EndTime,
                string.IsNullOrWhiteSpace(segment.SpeakerLabel)
                    ? language
                    : string.IsNullOrWhiteSpace(language)
                        ? segment.SpeakerLabel
                        : $"{segment.SpeakerLabel} · {language}"))
            .ToArray();
        return CreateSection(
            AnalyzedContentSectionKind.Transcript,
            GetString("AnalyzedContent_Transcript", "Transcript"),
            transcript.FullText,
            items,
            GetString("AnalyzedContent_NoSpeech", "No speech was detected."));
    }

    private AnalyzedContentSectionViewModel CreateImageTextSection(OcrDocumentV1 document)
    {
        AnalyzedContentItemViewModel[] items = document.Regions
            .SelectMany(region => region.Lines)
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .Select(line => new AnalyzedContentItemViewModel(
                line.Text,
                ActivateItem,
                imageBounds: line.Bounds))
            .ToArray();
        return CreateSection(
            AnalyzedContentSectionKind.ImageText,
            GetString("AnalyzedContent_Text", "Text"),
            document.FullText,
            items,
            GetString("AnalyzedContent_NoText", "No text was detected."));
    }

    private AnalyzedContentSectionViewModel CreateVideoTextSection(VideoOcrTrackV1 track)
    {
        AnalyzedContentItemViewModel[] items = track.Observations
            .Select(observation => new AnalyzedContentItemViewModel(
                observation.Text,
                ActivateItem,
                observation.StartTime,
                observation.EndTime))
            .ToArray();
        return CreateSection(
            AnalyzedContentSectionKind.VideoText,
            GetString("AnalyzedContent_OnScreenText", "On-screen text"),
            track.FullText,
            items,
            GetString("AnalyzedContent_NoText", "No text was detected."));
    }

    private AnalyzedContentSectionViewModel CreateVideoDescriptionSection(
        VideoDescriptionTrackV1 track)
    {
        AnalyzedContentItemViewModel[] items = track.Observations
            .Select(observation => new AnalyzedContentItemViewModel(
                observation.Description,
                ActivateItem,
                observation.StartTime,
                observation.EndTime))
            .ToArray();
        return CreateSection(
            AnalyzedContentSectionKind.VideoDescription,
            GetString("AnalyzedContent_VisualDescriptions", "Visual descriptions"),
            track.FullText,
            items,
            GetString("AnalyzedContent_NoDescription", "No description was generated."));
    }

    private AnalyzedContentSectionViewModel CreatePropertiesSection(MediaPropertiesV1 properties)
    {
        List<string> values = [];
        if (properties.PixelSize is PixelSize size)
        {
            values.Add($"{size.Width} × {size.Height} px");
        }

        if (properties.Duration is TimeSpan duration)
        {
            values.Add(AnalyzedContentItemViewModel.FormatTimecode(duration));
        }

        if (!string.IsNullOrWhiteSpace(properties.MimeType))
        {
            values.Add(properties.MimeType);
        }

        if (!string.IsNullOrWhiteSpace(properties.Container))
        {
            values.Add(properties.Container);
        }

        if (!string.IsNullOrWhiteSpace(properties.VideoCodec))
        {
            values.Add(properties.VideoCodec);
        }

        if (!string.IsNullOrWhiteSpace(properties.AudioCodec))
        {
            values.Add(properties.AudioCodec);
        }

        if (properties.AudioChannelCount is int audioChannelCount)
        {
            values.Add($"{audioChannelCount} ch");
        }

        if (properties.SampleRateHz is int sampleRateHz)
        {
            values.Add($"{sampleRateHz.ToString("N0", CultureInfo.CurrentCulture)} Hz");
        }

        if (properties.BitRate is long bitRate)
        {
            values.Add($"{bitRate.ToString("N0", CultureInfo.CurrentCulture)} bps");
        }

        if (properties.FrameRate is double frameRate)
        {
            values.Add($"{frameRate.ToString("0.##", CultureInfo.CurrentCulture)} fps");
        }

        return CreateTextSection(
            AnalyzedContentSectionKind.Properties,
            GetString("AnalyzedContent_Properties", "Properties"),
            string.Join(Environment.NewLine, values),
            GetString("AnalyzedContent_NoProperties", "No media properties are available."));
    }

    private AnalyzedContentSectionViewModel CreateTextSection(
        AnalyzedContentSectionKind kind,
        string title,
        string text,
        string emptyMessage)
    {
        return CreateSection(kind, title, text, [], emptyMessage);
    }

    private AnalyzedContentSectionViewModel CreateSection(
        AnalyzedContentSectionKind kind,
        string title,
        string fullText,
        IEnumerable<AnalyzedContentItemViewModel> items,
        string emptyMessage)
    {
        return new(kind, title, fullText, items, emptyMessage, CopySectionAsync);
    }

    private void AddSectionIfResult(AnalyzedContentSectionViewModel section)
    {
        if (section.HasResult)
        {
            _sections.Add(section);
        }
    }

    private static AnalyzedContentSectionViewModel CreateEmptySection()
    {
        return new(
            AnalyzedContentSectionKind.None,
            string.Empty,
            string.Empty,
            [],
            string.Empty,
            _ => Task.CompletedTask);
    }

    private void ActivateItem(AnalyzedContentItemViewModel item)
    {
        foreach (AnalyzedContentItemViewModel candidate in _sections.SelectMany(section => section.Items))
        {
            candidate.IsSelected = ReferenceEquals(candidate, item);
        }

        if (item.ImageBounds is PixelRect bounds)
        {
            ImageBoundsFocusRequested?.Invoke(this, bounds);
        }

        if (item.StartTime is TimeSpan start && item.IsSeekEnabled)
        {
            SeekRequested?.Invoke(this, start);
        }
    }

    private void ApplyInitialMatch()
    {
        CaptureMemoryMatchEvidence? match = _initialMatch;
        if (match == null || match.MatchKind == CaptureMemoryMatchKind.Filename)
        {
            return;
        }

        AnalyzedContentSectionKind kind = match.MatchKind switch
        {
            CaptureMemoryMatchKind.OcrText => AnalyzedContentSectionKind.ImageText,
            CaptureMemoryMatchKind.ImageDescription => AnalyzedContentSectionKind.ImageDescription,
            CaptureMemoryMatchKind.SpeechTranscript => AnalyzedContentSectionKind.Transcript,
            CaptureMemoryMatchKind.VideoOcrText => AnalyzedContentSectionKind.VideoText,
            CaptureMemoryMatchKind.VideoDescription => AnalyzedContentSectionKind.VideoDescription,
            _ => AnalyzedContentSectionKind.None,
        };
        AnalyzedContentSectionViewModel? section = _sections.FirstOrDefault(
            candidate => candidate.Kind == kind);
        if (section == null)
        {
            return;
        }

        SelectedSection = section;
        IsPaneOpen = true;
        AnalyzedContentItemViewModel? item = match.Timecode is TimeSpan timecode
            ? section.Items.FirstOrDefault(candidate => candidate.StartTime == timecode)
            : section.Items.FirstOrDefault(candidate =>
                candidate.Text.Contains(match.Snippet, StringComparison.CurrentCultureIgnoreCase) ||
                match.Snippet.Contains(candidate.Text, StringComparison.CurrentCultureIgnoreCase));
        if (item != null)
        {
            ActivateItem(item);
        }
        else if (match.PixelBounds is CaptureMemoryPixelBounds bounds)
        {
            ImageBoundsFocusRequested?.Invoke(
                this,
                new PixelRect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }

        _initialMatch = null;
    }

    private void UpdateSeekAvailability()
    {
        foreach (AnalyzedContentItemViewModel item in _sections.SelectMany(section => section.Items))
        {
            item.IsSeekEnabled = item.StartTime is TimeSpan start &&
                (!_minimumSeekTime.HasValue || start >= _minimumSeekTime.Value) &&
                (!_maximumSeekTime.HasValue || start <= _maximumSeekTime.Value);
        }
    }

    private bool CanReanalyzeAll()
    {
        return !IsReanalyzing &&
            _maintenance != null &&
            GetCaptureId().HasValue;
    }

    private bool CanReanalyzeSelected()
    {
        return CanReanalyzeAll() && GetCapability(SelectedSection.Kind).HasValue;
    }

    private async Task ReanalyzeAsync(bool selectedSectionOnly)
    {
        CaptureId? captureId = GetCaptureId();
        AnalysisCapabilityId? capabilityId = selectedSectionOnly
            ? GetCapability(SelectedSection.Kind)
            : null;
        if (_maintenance == null ||
            !captureId.HasValue ||
            selectedSectionOnly && !capabilityId.HasValue)
        {
            return;
        }

        IsReanalyzing = true;
        try
        {
            CaptureAnalysisMaintenanceResult result = await _maintenance
                .ReanalyzeCapturesAsync(
                    new CaptureAnalysisReanalysisRequest(
                        CaptureAnalysisReanalysisScope.SelectedCaptures,
                        [captureId.Value],
                        operationId: Guid.NewGuid(),
                        capabilityIds: capabilityId.HasValue ? [capabilityId.Value] : null),
                    CancellationToken.None)
                .ConfigureAwait(true);
            if (result.AffectedCaptureCount > 0)
            {
                _notifications?.ShowInfo(GetString(
                    selectedSectionOnly
                        ? "AnalyzedContent_ReanalyzeTabQueued"
                        : "AnalyzedContent_ReanalyzeAllQueued",
                    selectedSectionOnly
                        ? "This analysis was queued."
                        : "Capture reanalysis was queued."));
            }
            else
            {
                _notifications?.ShowError(GetString(
                    "AnalyzedContent_ReanalyzeFailed",
                    "This capture could not be queued for analysis."));
            }
        }
        catch
        {
            _notifications?.ShowError(GetString(
                "AnalyzedContent_ReanalyzeFailed",
                "This capture could not be queued for analysis."));
        }
        finally
        {
            IsReanalyzing = false;
        }
    }

    private CaptureId? GetCaptureId()
    {
        return _resolvedCaptureId ?? _request?.CaptureId;
    }

    private static AnalysisCapabilityId? GetCapability(AnalyzedContentSectionKind kind)
    {
        return kind switch
        {
            AnalyzedContentSectionKind.Transcript => AnalysisCapabilities.SpeechTranscriptV1.Id,
            AnalyzedContentSectionKind.ImageText => AnalysisCapabilities.OcrDocumentV1.Id,
            AnalyzedContentSectionKind.ImageDescription => AnalysisCapabilities.ImageDescriptionV1.Id,
            AnalyzedContentSectionKind.VideoText => AnalysisCapabilities.VideoOcrTrackV1.Id,
            AnalyzedContentSectionKind.VideoDescription => AnalysisCapabilities.VideoDescriptionTrackV1.Id,
            AnalyzedContentSectionKind.Properties => AnalysisCapabilities.MediaPropertiesV1.Id,
            _ => null,
        };
    }

    private void RequestImageTextVisibility()
    {
        ImageTextVisibilityRequested?.Invoke(
            this,
            IsPaneOpen && SelectedSection.Kind == AnalyzedContentSectionKind.ImageText);
    }

    private async Task CopySectionAsync(AnalyzedContentSectionViewModel section)
    {
        if (_clipboard == null || string.IsNullOrWhiteSpace(section.FullText))
        {
            return;
        }

        try
        {
            await _clipboard.CopyTextAsync(section.FullText);
            _notifications?.ShowInfo(GetString("AnalyzedContent_Copied", "Analyzed content copied."));
        }
        catch
        {
            _notifications?.ShowError(GetString("AnalyzedContent_CopyFailed", "Analyzed content could not be copied."));
        }
    }

    private void ChangeNotifier_AnalysisChanged(
        object? sender,
        CaptureAnalysisChangedEventArgs e)
    {
        if (!_resolvedCaptureId.HasValue || e.CaptureId == _resolvedCaptureId.Value)
        {
            RefreshCompletion = RefreshAsync();
        }
    }

    private void PostToCapturedContext(Action action)
    {
        if (_synchronizationContext == null ||
            ReferenceEquals(SynchronizationContext.Current, _synchronizationContext))
        {
            action();
            return;
        }

        _synchronizationContext.Post(static state => ((Action)state!).Invoke(), action);
    }

    private string GetString(string key, string fallback)
    {
        string? value = _localization?.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }
}
