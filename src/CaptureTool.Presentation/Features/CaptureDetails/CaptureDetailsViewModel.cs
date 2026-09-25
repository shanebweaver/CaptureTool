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
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed class CaptureDetailsViewModel : ViewModelBase
{
    private readonly ICaptureDetailsReader _reader;
    private readonly ICaptureNamingService? _names;
    private long _nameRevision;
    private readonly ICaptureMemoryService _memory;
    private readonly IClipboardService _clipboard;
    private readonly ILocalizationService _text;
    private readonly ITaskEnvironment _ui;
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

    public string PhysicalFileName => _path == null ? string.Empty : Path.GetFileName(_path);
    public string NameDraft { get; set => Set(ref field, value); } = string.Empty;
    public string NameStatus { get; private set => Set(ref field, value); } = string.Empty;
    public bool IsEditingName { get; private set => Set(ref field, value); }
    public bool CanEditName => _names != null;
    public IRelayCommand EditNameCommand { get; }
    public IRelayCommand CancelNameCommand { get; }
    public IAsyncRelayCommand SaveNameCommand { get; }
    public string FileName { get; private set => Set(ref field, value); } = string.Empty;
    public string FilePath => _path ?? string.Empty;
    public CaptureFileProperties FileProperties { get; private set => Set(ref field, value); } = new();
    public string FileStatus { get; private set => Set(ref field, value); } = string.Empty;
    public bool IsReadingFile { get; private set => Set(ref field, value); }
    public bool HasEdits { get; set => Set(ref field, value); }
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
    public string CopyStatus { get; private set => Set(ref field, value); } = string.Empty;
    public bool ShowStatus => StatusText.Length > 0;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<string> CopyCommand { get; }

    public CaptureDetailsViewModel(ICaptureDetailsReader reader, ICaptureMemoryService memory, IClipboardService clipboard,
        ILocalizationService text, ITaskEnvironment ui, IFolderLauncher? folders = null, ICaptureNamingService? names = null)
    {
        _reader = reader; _memory = memory; _clipboard = clipboard; _text = text; _ui = ui;
        _folders = folders;
        _names = names;
        EditNameCommand = new RelayCommand(() => { NameDraft = FileName == PhysicalFileName ? Path.GetFileNameWithoutExtension(FileName) : FileName; NameStatus = string.Empty; IsEditingName = true; });
        CancelNameCommand = new RelayCommand(() => { IsEditingName = false; NameStatus = string.Empty; });
        SaveNameCommand = new AsyncRelayCommand(SaveNameAsync);
        if (_names != null) _names.Changed += OnNamesChanged;
        TextContent = new(text);
        CopyResultsCommand = new AsyncRelayCommand(() => CopyAsync(TextContent.CopyVisibleScope()));
        _cancellation = _lifetime.Token;
        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync, () => !_disposed && !IsReading && !IsReadingFile);
        CopyCommand = new AsyncRelayCommand<string>(CopyAsync, value => !_disposed && !string.IsNullOrEmpty(value));
        CopyPathCommand = new RelayCommand(() => _ = CopyPathAsync());
        OpenFolderCommand = new RelayCommand(OpenFolder);
        _memory.StateChanged += OnMemoryChanged;
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
        var name = await _names.GetNameAsync(_path, _cancellation);
        if (!_disposed && revision == Interlocked.Read(ref _nameRevision)) FileName = name?.Text ?? PhysicalFileName;
    }
    private async Task SaveNameAsync()
    {
        if (_names == null || _path == null || _kind == null || _disposed) return;
        try { _ = new CaptureName(NameDraft, false); }
        catch (ArgumentException) { NameStatus = _text.GetString("CaptureNaming_Invalid"); return; }
        CaptureFileType kind = _kind switch { AnalysisMediaKind.Image => CaptureFileType.Image, AnalysisMediaKind.Audio => CaptureFileType.Audio, _ => CaptureFileType.Video };
        bool saved = await _names.SetNameAsync(_path, kind, NameDraft, _cancellation);
        if (_disposed) return;
        if (saved) { IsEditingName = false; NameStatus = string.Empty; await ReadNameAsync(); }
        else NameStatus = _text.GetString("CaptureNaming_SaveFailed");
    }

    private async Task ReadFileAsync()
    {
        if (_disposed || _path == null || _kind == null) return;
        if (IsReadingFile) { _readFileAgain = true; return; }
        IsReadingFile = true;
        RefreshCommand.NotifyCanExecuteChanged();
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
        finally { IsReadingFile = false; RefreshCommand.NotifyCanExecuteChanged(); }
    }

    private async Task CopyPathAsync()
    {
        if (_disposed || _path == null) return;
        try { await _clipboard.CopyTextAsync(_path); if (!_disposed) CopyStatus = Text("Copied"); }
        catch (Exception) { if (!_disposed) CopyStatus = Text("CopyFailed"); }
    }

    private void OpenFolder()
    {
        if (_disposed || _path == null) return;
        try
        {
            if (Path.GetDirectoryName(_path) is not { } directory || _folders?.TryOpenFolder(directory) != true)
                CopyStatus = _text.GetString("CapturePane_FolderUnavailable");
        }
        catch (Exception) { CopyStatus = _text.GetString("CapturePane_FolderUnavailable"); }
    }

    private void SetSummary(CaptureAnalysisRecord? record)
    {
        Summary = Content.Summary;
        TextContent.Replace(Content.Passages);
        TextContent.SetNavigationContext(_navigation with { SourceMatches = _sourceMatches && record != null });
        RaisePropertyChanged(nameof(HasSummary));
        RaisePropertyChanged(nameof(SummaryNotice));
    }

    public void SetNavigationContext(CaptureTextNavigationContext context)
    {
        _navigation = context;
        TextContent.SetNavigationContext(context with { SourceMatches = _sourceMatches });
    }
    public void ReportActionFailure() => CopyStatus = _text.GetString("CapturePane_ActionFailed");

    public async Task RefreshAsync()
    {
        if (_disposed || _path == null) return;
        if (IsReading) { _readAgain = true; return; }
        IsReading = true;
        RefreshCommand.NotifyCanExecuteChanged();
        try
        {
            do
            {
                _readAgain = false;
                long revision = Interlocked.Read(ref _revision);
                if (_memory.State.IsDeleting)
                {
                    Content = new();
                    SetSummary(null);
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
                    Content = content;
                    SetSummary(snapshot.Record);
                    SetStatus(Status(snapshot, content));
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { return; }
                catch (Exception)
                {
                    if (_disposed || revision != Interlocked.Read(ref _revision)) continue;
                    Content = new();
                    SetSummary(null);
                    SetStatus(Text("Unavailable"));
                }
            } while (_readAgain && !_disposed);
        }
        finally
        {
            IsReading = false;
            RefreshCommand.NotifyCanExecuteChanged();
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
        if (!content.HasContent && snapshot.Run?.IsPending != true) messages.Add(Text("Empty"));
        if (content.HasRetainedResults) messages.Add(Text("Retained"));
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
            if (state.IsDeleting || state.Storage.HasData == false)
            {
                Content = new();
                SetSummary(null);
                CopyStatus = string.Empty;
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
            if (!_disposed) CopyStatus = Text("Copied");
        }
        catch (Exception) { if (!_disposed) CopyStatus = Text("CopyFailed"); }
    }
    private string Text(string key) => _text.GetString("CaptureDetails_" + key);
    private void SetStatus(string value) { StatusText = value; RaisePropertyChanged(nameof(ShowStatus)); RaisePropertyChanged(nameof(SummaryNotice)); }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.StateChanged -= OnMemoryChanged;
        if (_names != null) _names.Changed -= OnNamesChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Content = new();
        SetSummary(null);
        FileProperties = new();
        CopyStatus = string.Empty;
        base.Dispose();
    }
}
