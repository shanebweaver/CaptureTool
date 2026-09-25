using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed class CaptureDetailsViewModel : ViewModelBase
{
    private readonly ICaptureDetailsReader _reader;
    private readonly ICaptureMemoryService _memory;
    private readonly IClipboardService _clipboard;
    private readonly ILocalizationService _text;
    private readonly ITaskEnvironment _ui;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _cancellation;
    private bool _disposed;
    private bool _readAgain;
    private long _revision;
    private object? _stateKey;
    private string? _path;

    public string FileName { get; private set => Set(ref field, value); } = string.Empty;
    public CaptureDetailsContent Content { get; private set => Set(ref field, value); } = new();
    public bool IsReading { get; private set => Set(ref field, value); }
    public string StatusText { get; private set => Set(ref field, value); } = string.Empty;
    public string CopyStatus { get; private set => Set(ref field, value); } = string.Empty;
    public bool ShowStatus => StatusText.Length > 0;
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<string> CopyCommand { get; }

    public CaptureDetailsViewModel(ICaptureDetailsReader reader, ICaptureMemoryService memory, IClipboardService clipboard,
        ILocalizationService text, ITaskEnvironment ui)
    {
        _reader = reader; _memory = memory; _clipboard = clipboard; _text = text; _ui = ui;
        _cancellation = _lifetime.Token;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !_disposed && !IsReading);
        CopyCommand = new AsyncRelayCommand<string>(CopyAsync, value => !_disposed && !string.IsNullOrEmpty(value));
        _memory.StateChanged += OnMemoryChanged;
    }

    public Task OpenAsync(string path)
    {
        if (_path != null) throw new InvalidOperationException("Each details view has its own lifetime.");
        _path = path;
        FileName = Path.GetFileName(path);
        return RefreshAsync();
    }

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
                    SetStatus(Text("Deleting"));
                    break;
                }
                CaptureDetailsSnapshot snapshot;
                try
                {
                    // Catalog discovery and payload projection can be substantial; keep them off the UI thread.
                    snapshot = await Task.Run(() => _reader.ReadAsync(_path, _cancellation), _cancellation);
                    var content = await Task.Run(() => snapshot.Record == null ? new CaptureDetailsContent() :
                        CaptureDetailsContent.Create(snapshot.Record, _text), _cancellation);
                    if (_disposed || revision != Interlocked.Read(ref _revision)) continue;
                    Content = content;
                    SetStatus(Status(snapshot, content));
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { return; }
                catch (Exception)
                {
                    if (_disposed || revision != Interlocked.Read(ref _revision)) continue;
                    Content = new();
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
    private void SetStatus(string value) { StatusText = value; RaisePropertyChanged(nameof(ShowStatus)); }
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.StateChanged -= OnMemoryChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Content = new();
        CopyStatus = string.Empty;
        base.Dispose();
    }
}
