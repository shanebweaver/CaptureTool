using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain.Capture;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed class CaptureDetailsViewModel : ViewModelBase
{
    private readonly ICaptureDetailsReader _reader;
    private readonly ICaptureNamingService? _names;
    private long _nameRevision;
    public event Action<CaptureFileRename>? FileRenamed;
    public string SuggestedName { get; private set => Set(ref field, value); } = string.Empty;
    public bool HasSuggestedName => SuggestedName.Length > 0;
    public bool IsGeneratingName { get; private set => Set(ref field, value); }
    public bool IsGeneratingSummary { get; private set => Set(ref field, value); }
    private readonly ICaptureMemoryService _memory;
    private readonly ICaptureAnalysisOnboarding? _onboarding;
    public CaptureAnalysisAction AltTextAction { get; }
    public CaptureAnalysisAction SummaryAction { get; }
    public CaptureAnalysisAction NameAction { get; }
    public CaptureAnalysisAction TextAction { get; }
    public CaptureAnalysisAction QrAction { get; }
    public CaptureAnalysisAction TranscriptAction { get; }
    private IEnumerable<CaptureAnalysisAction> Actions => [AltTextAction, SummaryAction, NameAction, TextAction, QrAction, TranscriptAction];
    private CaptureDetailsSnapshot? _snapshot;
    private bool _requesting;
    private readonly HashSet<AnalysisCapability> _unavailableActions = [];
    private bool _acceptedName;
    private bool _reviewSuggestedName;
    public bool HasVisualMedia => _kind is AnalysisMediaKind.Image or AnalysisMediaKind.Video;
    public bool HasAudio => _kind is AnalysisMediaKind.Audio or AnalysisMediaKind.Video;
    public bool IsImage => _kind == AnalysisMediaKind.Image;
    public bool NeedsSummaryInputs => !IsImage && !Content.HasSummaryInputs;
    private readonly IClipboardService _clipboard;
    private readonly ILocalizationService _text;
    private readonly ITaskEnvironment _ui;
    private readonly IAppNotificationService _notifications;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _cancellation;
    private bool _disposed;
    private bool _readAgain;
    private bool _readFileAgain;
    private long _revision;
    private object? _stateKey;
    private string? _path;
    private AnalysisMediaKind? _kind;
    private string? _workingPath;
    private bool _sourceMatches;
    private CaptureTextNavigationContext _navigation = new(false, false);
    private readonly IFolderLauncher? _folders;
    private CaptureEditorTextSession? _editorText;
    private RecognizedTextDocument? _editorDocument;
    private IReadOnlyList<CaptureTextPassage> _editorPassages = [];
    private bool _wasPolicyAllowed;
    private bool UsesEditorText => IsImage && _editorText != null && (_editorText.HasChanges || _editorText.Document != null ||
        _snapshot?.Status is CaptureDetailsStatus.SourceChanged or CaptureDetailsStatus.SourceUnavailable ||
        _snapshot?.Record != null && !_sourceMatches);
    public bool ShowQrAction => HasVisualMedia && !UsesEditorText;
    public IReadOnlyList<CaptureTextPassage> TextPassages => UsesEditorText ? _editorPassages : Content.Passages;
    public string TextStatus => UsesEditorText ? string.Empty : StatusText;

    public string PhysicalFileName => _path == null ? string.Empty : Path.GetFileName(_path);
    public string NameDraft { get; set => Set(ref field, value); } = string.Empty;
    public string NameStatus { get; private set => Set(ref field, value); } = string.Empty;
    public bool IsEditingName { get; private set => Set(ref field, value); }
    public bool CanEditName => _names != null;
    public IRelayCommand EditNameCommand { get; }
    public IRelayCommand CancelNameCommand { get; }
    public IAsyncRelayCommand SaveNameCommand { get; }
    public IAsyncRelayCommand SuggestNameCommand { get; }
    public string NameSuggestionToolTip => IsGeneratingName ? _text.GetString("CaptureNaming_AiGenerating")
        : NameAction.HasStatus ? NameAction.Status : _text.GetString("CaptureNaming_Suggest");
    public string FileName { get; private set => Set(ref field, value); } = string.Empty;
    public string FilePath => _path ?? string.Empty;
    public CaptureFileProperties FileProperties { get; private set => Set(ref field, value); } = new();
    public string FileStatus { get; private set => Set(ref field, value); } = string.Empty;
    public bool IsReadingFile { get; private set => Set(ref field, value); }
    public bool HasEdits { get; set => Set(ref field, value); }
    public bool CanShowImageTextOverlay => IsImage && _navigation.IsReady && !_memory.State.IsDeleting &&
        (UsesEditorText ? _editorText!.Document != null : _sourceMatches && !_navigation.ImageEdited);
    public string Summary { get; private set => Set(ref field, value); } = string.Empty;
    public bool HasSummary => Summary.Length > 0;
    public string SummaryNotice => HasSummary ? StatusText : string.Empty;
    public IRelayCommand CopyPathCommand { get; }
    public IRelayCommand OpenFolderCommand { get; }
    public CaptureTextViewModel TextContent { get; }
    public string ContentTabTitle => _text.GetString(_kind == AnalysisMediaKind.Audio ? "CaptureDetails_Transcript" : "CapturePane_TextTab");
    public IAsyncRelayCommand CopyResultsCommand { get; }
    public CaptureDetailsContent Content { get; private set => Set(ref field, value); } = new();
    public bool IsReading { get; private set => Set(ref field, value); }
    public string StatusText { get; private set => Set(ref field, value); } = string.Empty;
    public bool ShowStatus => StatusText.Length > 0;
    public IAsyncRelayCommand<string> CopyCommand { get; }

    public CaptureDetailsViewModel(ICaptureDetailsReader reader, ICaptureMemoryService memory, IClipboardService clipboard,
        ILocalizationService text, ITaskEnvironment ui, IAppNotificationService notifications,
        IFolderLauncher? folders = null, ICaptureNamingService? names = null, ICaptureAnalysisOnboarding? onboarding = null)
    {
        _reader = reader; _memory = memory; _clipboard = clipboard; _text = text; _ui = ui;
        _wasPolicyAllowed = memory.State.Policy.IsAllowed;
        _notifications = notifications;
        _folders = folders;
        _names = names;
        _onboarding = onboarding;
        AltTextAction = Action(AnalysisCapability.ImageAltText, "AltText");
        SummaryAction = Action(AnalysisCapability.CaptureSynopsis, "Summary");
        NameAction = Action(AnalysisCapability.CaptureName, "Name");
        TextAction = Action(AnalysisCapability.TextRecognition, "Text");
        QrAction = Action(AnalysisCapability.QrCodeDetection, "Qr");
        TranscriptAction = Action(AnalysisCapability.Transcription, "Transcript");
        EditNameCommand = new RelayCommand(() => { _reviewSuggestedName = false; EditName(FileName == PhysicalFileName ? Path.GetFileNameWithoutExtension(FileName) : FileName); });
        CancelNameCommand = new RelayCommand(() => { _reviewSuggestedName = false; IsEditingName = false; NameStatus = string.Empty; });
        SaveNameCommand = new AsyncRelayCommand(() => RenameAsync(NameDraft));
        SuggestNameCommand = new AsyncRelayCommand(SuggestNameAsync, () => !_disposed && CanEditName &&
            !_memory.State.IsDeleting && !IsGeneratingName && (HasSuggestedName || NameAction.Command.CanExecute(null)));
        if (_names != null) _names.Changed += OnNamesChanged;
        TextContent = new(text);
        CopyResultsCommand = new AsyncRelayCommand(() => CopyAsync(TextContent.CopyVisibleScope()));
        _cancellation = _lifetime.Token;
        CopyCommand = new AsyncRelayCommand<string>(CopyAsync, value => !_disposed && !string.IsNullOrEmpty(value));
        CopyPathCommand = new RelayCommand(() => _ = CopyPathAsync());
        OpenFolderCommand = new RelayCommand(OpenFolder);
        _memory.StateChanged += OnMemoryChanged;

        CaptureAnalysisAction Action(AnalysisCapability capability, string key) =>
            new(capability, "CaptureAction_" + key, _text.GetString("CaptureAction_" + key), () => RequestAnalysisAsync(capability));
    }

    private void EditName(string name)
    {
        NameDraft = name;
        NameStatus = string.Empty;
        IsEditingName = true;
    }

    private async Task SuggestNameAsync()
    {
        if (HasSuggestedName) { EditName(SuggestedName); return; }
        _reviewSuggestedName = true;
        await NameAction.Command.ExecuteAsync(null);
        // A memory update may already be reading the queued run or its result.
        if (!IsReading && !IsGeneratingName) _reviewSuggestedName = false;
    }

    private async Task RequestAnalysisAsync(AnalysisCapability capability)
    {
        if (!_disposed && UsesEditorText && (capability == AnalysisCapability.TextRecognition || capability == AnalysisCapability.QrCodeDetection))
        {
            await _editorText!.ExtractAsync();
            return;
        }
        if (_disposed || _path == null || _requesting || _memory.State.IsDeleting || _snapshot?.Run?.IsPending == true) return;
        if (NeedsSummaryInputs && (capability == AnalysisCapability.CaptureSynopsis || capability == AnalysisCapability.CaptureName)) return;
        _requesting = true;
        _unavailableActions.Remove(capability);
        UpdateActions();
        try
        {
            bool consent = _onboarding != null ? await _onboarding.EnableAsync(_cancellation) : await _memory.EnsureConsentAsync(_cancellation);
            if (_disposed) return;
            if (!consent)
            {
                if (!_memory.State.PolicyAvailable) _notifications.ShowError(Text("Unavailable"));
                return;
            }
            await _memory.AnalyzeAsync(_path, capability, _cancellation);
            if (_memory.State.FailureCode is { } failure && !_disposed)
            {
                if (failure == "model-unavailable")
                {
                    _unavailableActions.Add(capability);
                    _notifications.ShowInfo(_text.GetString("CaptureAction_Unavailable"));
                }
                else _notifications.ShowError(Text("Unavailable"));
            }
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed) _notifications.ShowError(Text("Unavailable")); }
        finally { _requesting = false; if (!_disposed) UpdateActions(); }
    }

    private void UpdateActions()
    {
        bool pending = _snapshot?.Run?.IsPending == true;
        foreach (var action in Actions)
        {
            if (UsesEditorText && (action == TextAction || action == QrAction))
            {
                var document = _editorText!.Document;
                bool isText = action == TextAction;
                bool hasResult = isText ? document?.HasTextResults == true : document?.HasQrCodeResults == true;
                string editorStatus = isText ? _editorText.Status : string.Empty;
                if (!hasResult && !_editorText.CanExtract && editorStatus.Length == 0)
                    editorStatus = _text.GetString("CaptureAction_Unavailable");
                if (hasResult && (isText ? !_editorPassages.Any(passage => passage.Source == CaptureTextSource.ImageText) : document!.QrCodes.Count == 0))
                    editorStatus = _text.GetString(isText ? "CaptureAction_NoText" : "CaptureAction_NoQr");
                action.Update(isText && _editorText.IsRunning, hasResult,
                    !_disposed && !_memory.State.IsDeleting && _editorText.CanExtract && !_editorText.IsRunning, editorStatus);
                continue;
            }
            bool running = pending && _memory.State.Policy.IsAllowed && _snapshot!.Run!.Steps.Contains(action.Capability) &&
                !_snapshot.Run.CompletedSteps.Any(step => step.Capability == action.Capability);
            var result = _snapshot?.Record?.Results.SingleOrDefault(result => result.Payload.Capability == action.Capability);
            string? status = result?.Payload switch
            {
                TextRecognitionMetadata { Regions.Count: 0 } => "NoText",
                QrCodeMetadata { Codes.Count: 0 } => "NoQr",
                TranscriptMetadata { Segments.Count: 0 } => "NoSpeech",
                DescriptionMetadata { Descriptions.Count: 0 } => "NoDescription",
                ImageAltTextMetadata { Suggestion: null } => "NoSuggestion",
                CaptureSynopsisMetadata { Summary.Count: 0 } when action == SummaryAction => "NoSuggestion",
                CaptureNameMetadata { Suggestion: null } when !_acceptedName => "NoSuggestion",
                _ => null
            };
            var outcome = _snapshot?.Run?.CompletedSteps.SingleOrDefault(step => step.Capability == action.Capability)?.Outcome;
            if (result == null && !running && outcome != null)
                status = outcome == AnalyzerOutcomeKind.Unsupported ? "Unavailable" : "Failed";
            if (result == null && !running && _unavailableActions.Contains(action.Capability)) status = "Unavailable";
            action.Update(running, result != null || action == NameAction && (_acceptedName || Content.SuggestedName.Length > 0),
                !_disposed && !_requesting && !pending && !_memory.State.IsDeleting &&
                (!(action.Capability == AnalysisCapability.CaptureSynopsis || action.Capability == AnalysisCapability.CaptureName) || !NeedsSummaryInputs) &&
                (action.Capability != AnalysisCapability.ImageAltText || IsImage),
                status == null ? string.Empty : _text.GetString("CaptureAction_" + status));
        }
        SummaryAction.Label = _text.GetString(IsImage ? "CaptureAction_ScreenshotSummary" : "CaptureAction_Summary");
        RaisePropertyChanged(nameof(IsImage));
        IsGeneratingSummary = SummaryAction.IsRunning;
        IsGeneratingName = NameAction.IsRunning;
        RaisePropertyChanged(nameof(NameSuggestionToolTip));
        SuggestNameCommand.NotifyCanExecuteChanged();
        RaisePropertyChanged(nameof(HasVisualMedia));
        RaisePropertyChanged(nameof(HasAudio));
        RaisePropertyChanged(nameof(NeedsSummaryInputs));
    }

    public Task OpenAsync(string path)
    {
        if (_path != null) throw new InvalidOperationException("Each details view has its own lifetime.");
        _path = path;
        RaisePropertyChanged(nameof(PhysicalFileName));
        RaisePropertyChanged(nameof(FilePath));
        FileName = Path.GetFileName(path);
        return Task.WhenAll(RefreshAsync(), ReadNameAsync());
    }

    public Task OpenAsync(string path, AnalysisMediaKind kind, string? workingPath = null)
    {
        _kind = kind;
        UpdateActions();
        _workingPath = workingPath;
        RaisePropertyChanged(nameof(ContentTabTitle));
        Task analysis = OpenAsync(path);
        return Task.WhenAll(analysis, ReadFileAsync());
    }

    public Task RefreshAllAsync() => Task.WhenAll(RefreshAsync(), ReadFileAsync(), ReadNameAsync());

    private void OnNamesChanged() => _ui.TryExecute(() => { if (!_disposed) _ = ReadNameAsync(); });
    private async Task ReadNameAsync()
    {
        if (_names == null || _path == null || _disposed) return;
        long revision = Interlocked.Increment(ref _nameRevision);
        var state = await _names.GetStateAsync(_path, _cancellation);
        if (_disposed || revision != Interlocked.Read(ref _nameRevision)) return;
        FileName = PhysicalFileName;
        _acceptedName = state?.Name is { IsAutomatic: false };
        SuggestedName = _acceptedName ? string.Empty : Content.SuggestedName.Length > 0
            ? new CaptureName(Content.SuggestedName, true).SuggestedFileName()
            : state?.Name is { IsAutomatic: true } name ? name.SuggestedFileName() : string.Empty;
        RaisePropertyChanged(nameof(HasSuggestedName));
        if (_reviewSuggestedName && HasSuggestedName)
        {
            _reviewSuggestedName = false;
            EditName(SuggestedName);
        }
        UpdateActions();
    }
    private async Task RenameAsync(string name)
    {
        if (_names == null || _path == null || _kind == null || _disposed) return;
        try { _ = new CaptureName(name, false); }
        catch (ArgumentException) { NameStatus = _text.GetString("CaptureNaming_Invalid"); return; }
        CaptureFileType kind = _kind switch { AnalysisMediaKind.Image => CaptureFileType.Image, AnalysisMediaKind.Audio => CaptureFileType.Audio, _ => CaptureFileType.Video };
        // Once accepted, finish the filesystem transaction even if the pane closes.
        var renamed = await _names.RenameAsync(_path, kind, name, CancellationToken.None);
        if (renamed == null) { if (!_disposed) NameStatus = _text.GetString("CaptureNaming_SaveFailed"); return; }
        if (_workingPath == _path) _workingPath = renamed.NewPath;
        _path = renamed.NewPath;
        FileName = PhysicalFileName;
        RaisePropertyChanged(nameof(PhysicalFileName));
        RaisePropertyChanged(nameof(FilePath));
        IsEditingName = false;
        SuggestedName = string.Empty;
        RaisePropertyChanged(nameof(HasSuggestedName));
        NameStatus = string.Empty;
        FileRenamed?.Invoke(renamed);
        if (!_disposed) await RefreshAllAsync();
    }

    private async Task ReadFileAsync()
    {
        if (_disposed || _path == null || _kind == null) return;
        if (IsReadingFile) { _readFileAgain = true; return; }
        IsReadingFile = true;
        try
        {
            do
            {
                _readFileAgain = false;
                var file = await Task.Run(() => _reader.ReadFileAsync(_path, _kind.Value, _cancellation), _cancellation);
                if (_disposed) return;
                FileProperties = file == null ? new() : CaptureFileProperties.Create(file, _text);
                FileStatus = file == null ? _text.GetString("CapturePane_FileUnavailable") : string.Empty;
            } while (_readFileAgain && !_disposed);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception) { if (!_disposed) FileStatus = _text.GetString("CapturePane_FileUnavailable"); }
        finally { IsReadingFile = false; }
    }

    private async Task CopyPathAsync()
    {
        if (_disposed || _path == null) return;
        try { await _clipboard.CopyTextAsync(_path); if (!_disposed) _notifications.ShowInfo(Text("Copied")); }
        catch (Exception) { if (!_disposed) _notifications.ShowError(Text("CopyFailed")); }
    }

    private void OpenFolder()
    {
        if (_disposed || _path == null) return;
        try
        {
            if (Path.GetDirectoryName(_path) is not { } directory || _folders?.TryOpenFolder(directory) != true)
                _notifications.ShowError(_text.GetString("CapturePane_FolderUnavailable"));
        }
        catch (Exception) { _notifications.ShowError(_text.GetString("CapturePane_FolderUnavailable")); }
    }

    private void SetSummary(CaptureAnalysisRecord? record)
    {
        Summary = Content.Summary;
        RefreshText();
        RaisePropertyChanged(nameof(HasSummary));
        RaisePropertyChanged(nameof(SummaryNotice));
        UpdateActions();
    }

    public void SetNavigationContext(CaptureTextNavigationContext context)
    {
        _navigation = context;
        RefreshText();
    }

    public void SetEditorText(CaptureEditorTextSession? session)
    {
        if (ReferenceEquals(_editorText, session)) return;
        if (_editorText != null) _editorText.Changed -= EditorTextChanged;
        _editorText = session;
        if (_editorText != null) _editorText.Changed += EditorTextChanged;
        EditorTextChanged();
    }

    public async Task EnsureTextAsync()
    {
        if (_disposed || TextAction.HasResult || TextAction.IsRunning) return;
        if (TextAction.Command.CanExecute(null)) await TextAction.Command.ExecuteAsync(null);
    }

    private void EditorTextChanged()
    {
        if (_disposed) return;
        if (!ReferenceEquals(_editorDocument, _editorText?.Document))
        {
            _editorDocument = _editorText?.Document;
            _editorPassages = _editorDocument == null ? [] : CaptureTextPassage.From(_editorDocument, _text);
        }
        RefreshText();
        UpdateActions();
    }

    private void RefreshText()
    {
        TextContent.Replace(TextPassages);
        TextContent.SetNavigationContext(UsesEditorText
            ? new(_navigation.IsReady, _editorText!.Document != null)
            : _navigation with { SourceMatches = _sourceMatches });
        RaisePropertyChanged(nameof(TextPassages));
        RaisePropertyChanged(nameof(TextStatus));
        RaisePropertyChanged(nameof(ShowQrAction));
        RaisePropertyChanged(nameof(CanShowImageTextOverlay));
    }
    public void ReportActionFailure()
    {
        if (!_disposed) _notifications.ShowError(_text.GetString("CapturePane_ActionFailed"));
    }

    public async Task RefreshAsync()
    {
        if (_disposed || _path == null) return;
        if (IsReading) { _readAgain = true; return; }
        IsReading = true;
        try
        {
            do
            {
                _readAgain = false;
                long revision = Interlocked.Read(ref _revision);
                if (_memory.State.IsDeleting)
                {
                    _snapshot = null;
                    Content = new();
                    SetSummary(null);
                    IsGeneratingSummary = false;
                    IsGeneratingName = false;
                    SetStatus(Text("Deleting"));
                    break;
                }
                CaptureDetailsSnapshot snapshot;
                try
                {
                    // Catalog discovery and payload projection can be substantial; keep them off the UI thread.
                    snapshot = await Task.Run(() => _reader.ReadAsync(_path, _cancellation), _cancellation);
                    bool sourceMatches = snapshot.Record != null && (_workingPath == null ||
                        string.Equals(_path, _workingPath, StringComparison.OrdinalIgnoreCase) ||
                        await Task.Run(() => _reader.VerifySourceAsync(_workingPath, snapshot.Record.SourceRevision, _cancellation), _cancellation));
                    var content = await Task.Run(() => snapshot.Record == null ? new CaptureDetailsContent() :
                        CaptureDetailsContent.Create(snapshot.Record, _text), _cancellation);
                    if (_disposed || revision != Interlocked.Read(ref _revision)) continue;
                    _sourceMatches = sourceMatches;
                    _snapshot = snapshot;
                    Content = content;
                    SetSummary(snapshot.Record);
                    SetStatus(Status(snapshot, content));
                    await ReadNameAsync();
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { return; }
                catch (Exception)
                {
                    if (_disposed || revision != Interlocked.Read(ref _revision)) continue;
                    Content = new();
                    _snapshot = null;
                    SetSummary(null);
                    SetStatus(Text("Unavailable"));
                    IsGeneratingSummary = false;
                    IsGeneratingName = false;
                }
            } while (_readAgain && !_disposed);
        }
        finally
        {
            IsReading = false;
        }
    }

    private string Status(CaptureDetailsSnapshot snapshot, CaptureDetailsContent content)
    {
        string? state = snapshot.Status switch
        {
            CaptureDetailsStatus.SourceChanged => "SourceChanged",
            CaptureDetailsStatus.SourceUnavailable => "SourceUnavailable",
            CaptureDetailsStatus.Unavailable => "Unavailable",
            _ => null
        };
        if (state != null) return Text(state);
        List<string> messages = [];
        if (snapshot.Run?.IsPending == true)
            messages.Add(Text(_memory.State.Policy.IsAllowed ? "Analyzing" : "Paused"));
        else if (snapshot.Run is { } run && (run.Status != AnalysisRunStatus.Completed ||
            run.CompletedSteps.Any(step => step.Outcome != AnalyzerOutcomeKind.Succeeded)))
            messages.Add(Text("Partial"));
        if (!content.HasContent && snapshot.Run?.IsPending != true && snapshot.Record?.Results.Count is not > 0) messages.Add(Text("Empty"));
        if (content.HasLimitedCoverage) messages.Add(Text("Limited"));
        return string.Join(" ", messages);
    }

    private void OnMemoryChanged()
    {
        var state = _memory.State;
        // Ignore token/progress fractions; refresh at durable step boundaries and policy/storage changes.
        object key = (state.Activity.CaptureId, state.Activity.Activity, state.Activity.CompletedSteps,
            state.Activity.LastRunStatus, state.Storage, state.IsDeleting, state.Policy.Revision);
        if (Equals(Interlocked.Exchange(ref _stateKey, key), key) || _disposed) return;
        Interlocked.Increment(ref _revision);
        _ui.TryExecute(() =>
        {
            if (_disposed) return;
            bool revoked = _wasPolicyAllowed && !state.Policy.IsAllowed;
            _wasPolicyAllowed = state.Policy.IsAllowed;
            if ((state.IsDeleting || revoked) && _editorText is { } editor && (editor.Document != null || editor.IsRunning))
                editor.Invalidate(editor.HasChanges);
            if (!state.Policy.IsAllowed) { IsGeneratingName = false; IsGeneratingSummary = false; }
            if (state.IsDeleting || state.Storage.HasData == false)
            {
                _snapshot = null;
                Content = new();
                SetSummary(null);
                IsGeneratingName = false;
                IsGeneratingSummary = false;
            }
            _ = RefreshAsync();
        });
    }

    private async Task CopyAsync(string? value)
    {
        if (_disposed || string.IsNullOrEmpty(value) || _memory.State.IsDeleting) return;
        try
        {
            await _clipboard.CopyTextAsync(value);
            if (!_disposed) _notifications.ShowInfo(Text("Copied"));
        }
        catch (Exception) { if (!_disposed) _notifications.ShowError(Text("CopyFailed")); }
    }
    private string Text(string key) => _text.GetString("CaptureDetails_" + key);
    private void SetStatus(string value) { StatusText = value; RaisePropertyChanged(nameof(ShowStatus)); RaisePropertyChanged(nameof(SummaryNotice)); RaisePropertyChanged(nameof(TextStatus)); }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_editorText != null) _editorText.Changed -= EditorTextChanged;
        _memory.StateChanged -= OnMemoryChanged;
        if (_names != null) _names.Changed -= OnNamesChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Content = new();
        SetSummary(null);
        FileProperties = new();
        base.Dispose();
    }
}
