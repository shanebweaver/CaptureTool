using CaptureTool.Application.Abstractions.Analysis.Memory;
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
        IAppNotificationService? notifications = null)
    {
        _metadata = metadata;
        _changeNotifier = changeNotifier;
        _clipboard = clipboard;
        _localization = localization;
        _notifications = notifications;
        Sections = new ReadOnlyObservableCollection<AnalyzedContentSectionViewModel>(_sections);
        TogglePaneCommand = new RelayCommand(() => IsPaneOpen = !IsPaneOpen);
        ClosePaneCommand = new RelayCommand(() => IsPaneOpen = false);
        SelectedSection = CreateEmptySection();
    }

    public event EventHandler<TimeSpan>? SeekRequested;

    public event EventHandler<PixelRect?>? ImageBoundsFocusRequested;

    public event EventHandler<CaptureMetadataViewSnapshot?>? MetadataChanged;

    public ReadOnlyObservableCollection<AnalyzedContentSectionViewModel> Sections { get; }

    public Task RefreshCompletion { get; private set; } = Task.CompletedTask;

    public IRelayCommand TogglePaneCommand { get; }

    public IRelayCommand ClosePaneCommand { get; }

    public bool IsPaneOpen
    {
        get;
        set => Set(ref field, value);
    }

    public IAsyncRelayCommand? ShowImageTextCommand
    {
        get;
        private set => Set(ref field, value);
    }

    public IAsyncRelayCommand? GenerateBriefImageDescriptionCommand
    {
        get;
        private set => Set(ref field, value);
    }

    public IAsyncRelayCommand? GenerateDetailedImageDescriptionCommand
    {
        get;
        private set => Set(ref field, value);
    }

    public IAsyncRelayCommand? GenerateDiagramImageDescriptionCommand
    {
        get;
        private set => Set(ref field, value);
    }

    public IAsyncRelayCommand? GenerateAccessibleImageDescriptionCommand
    {
        get;
        private set => Set(ref field, value);
    }

    public bool HasImageActions
    {
        get;
        private set => Set(ref field, value);
    }

    public bool HasContent
    {
        get;
        private set => Set(ref field, value);
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
                RaisePropertyChanged(nameof(IsImageTextSelected));
                RaisePropertyChanged(nameof(IsImageDescriptionSelected));
            }
        }
    }

    public bool HasSelectedItems => SelectedSection.HasItems;

    public bool ShowSelectedFullText => SelectedSection.ShowFullText;

    public bool ShowSelectedEmpty => SelectedSection.ShowEmpty;

    public bool IsImageTextSelected =>
        SelectedSection.Kind == AnalyzedContentSectionKind.ImageText;

    public bool IsImageDescriptionSelected =>
        SelectedSection.Kind == AnalyzedContentSectionKind.ImageDescription;

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

        RefreshCompletion = RefreshAsync();
    }

    public void ConfigureImageActions(
        IAsyncRelayCommand showImageTextCommand,
        IAsyncRelayCommand generateBriefImageDescriptionCommand,
        IAsyncRelayCommand generateDetailedImageDescriptionCommand,
        IAsyncRelayCommand generateDiagramImageDescriptionCommand,
        IAsyncRelayCommand generateAccessibleImageDescriptionCommand)
    {
        ShowImageTextCommand = showImageTextCommand;
        GenerateBriefImageDescriptionCommand = generateBriefImageDescriptionCommand;
        GenerateDetailedImageDescriptionCommand = generateDetailedImageDescriptionCommand;
        GenerateDiagramImageDescriptionCommand = generateDiagramImageDescriptionCommand;
        GenerateAccessibleImageDescriptionCommand = generateAccessibleImageDescriptionCommand;
        HasImageActions = true;
    }

    public void SetCurrentImageText(
        string fullText,
        IEnumerable<(string Text, PixelRect Bounds)> lines)
    {
        AnalyzedContentItemViewModel[] items = lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .Select(line => new AnalyzedContentItemViewModel(
                line.Text,
                ActivateItem,
                imageBounds: line.Bounds))
            .ToArray();
        ReplaceSection(CreateSection(
            AnalyzedContentSectionKind.ImageText,
            GetString("AnalyzedContent_Text", "Text"),
            fullText,
            items,
            GetString("AnalyzedContent_NoText", "No text was detected.")));
    }

    public void SetCurrentImageDescription(string description)
    {
        ReplaceSection(CreateTextSection(
            AnalyzedContentSectionKind.ImageDescription,
            GetString("AnalyzedContent_Description", "Description"),
            description,
            GetString("AnalyzedContent_NoDescription", "No description was generated.")));
    }

    public void ClearImageDerivedContent()
    {
        AnalyzedContentSectionKind selectedKind = SelectedSection.Kind;
        for (int index = _sections.Count - 1; index >= 0; index--)
        {
            if (_sections[index].Kind is AnalyzedContentSectionKind.ImageText or
                AnalyzedContentSectionKind.ImageDescription)
            {
                _sections.RemoveAt(index);
            }
        }

        HasContent = _sections.Count > 0;
        if (selectedKind is AnalyzedContentSectionKind.ImageText or
            AnalyzedContentSectionKind.ImageDescription)
        {
            SelectedSection = _sections.FirstOrDefault() ?? CreateEmptySection();
        }
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
        _resolvedCaptureId = snapshot?.CaptureId;
        _sections.Clear();
        if (snapshot != null)
        {
            AddSections(snapshot);
        }

        HasContent = _sections.Count > 0;
        SelectedSection = _sections.FirstOrDefault() ?? CreateEmptySection();
        MetadataChanged?.Invoke(this, snapshot);
        ApplyInitialMatch();
        UpdateSeekAvailability();
    }

    private void AddSections(CaptureMetadataViewSnapshot snapshot)
    {
        if (snapshot.SpeechTranscript is SpeechTranscriptV1 transcript)
        {
            _sections.Add(CreateTranscriptSection(transcript));
        }

        if (snapshot.ImageText is OcrDocumentV1 imageText)
        {
            _sections.Add(CreateImageTextSection(imageText));
        }

        if (snapshot.ImageDescription is ImageDescriptionV1 imageDescription)
        {
            _sections.Add(CreateTextSection(
                AnalyzedContentSectionKind.ImageDescription,
                GetString("AnalyzedContent_Description", "Description"),
                imageDescription.Description,
                GetString("AnalyzedContent_NoDescription", "No description was generated.")));
        }

        if (snapshot.VideoText is VideoOcrTrackV1 videoText)
        {
            _sections.Add(CreateVideoTextSection(videoText));
        }

        if (snapshot.VideoDescription is VideoDescriptionTrackV1 videoDescription)
        {
            _sections.Add(CreateVideoDescriptionSection(videoDescription));
        }

        if (snapshot.MediaProperties is MediaPropertiesV1 properties)
        {
            _sections.Add(CreatePropertiesSection(properties));
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

    private void ReplaceSection(AnalyzedContentSectionViewModel replacement)
    {
        bool wasSelected = SelectedSection.Kind == replacement.Kind;
        int index = _sections
            .Select((section, sectionIndex) => (section, sectionIndex))
            .Where(value => value.section.Kind == replacement.Kind)
            .Select(value => value.sectionIndex)
            .DefaultIfEmpty(-1)
            .First();
        if (index >= 0)
        {
            _sections[index] = replacement;
        }
        else
        {
            _sections.Add(replacement);
        }

        HasContent = true;
        if (wasSelected || SelectedSection.Kind == AnalyzedContentSectionKind.None)
        {
            SelectedSection = replacement;
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
