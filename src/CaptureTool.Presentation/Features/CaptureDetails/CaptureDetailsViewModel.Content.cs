using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed partial class CaptureDetailsViewModel
{
    private readonly ICaptureDetailsReader _reader;
    private CaptureDetailsSnapshot? _snapshot;
    private string? _readNotice;
    private bool _fileUnavailable;
    private bool _readAgain;
    private bool _readFileAgain;
    private long _revision;
    private object? _stateKey;
    private bool _sourceMatches;
    private bool _wasPolicyAllowed;

    public CaptureFileProperties FileProperties
    {
        get;
        private set => Set(ref field, value);
    } = new();

    public bool IsReadingFile
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(IsReadingDetails));
            }
        }
    }

    public string Summary
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public bool HasSummary => Summary.Length > 0;

    public bool IsSummaryPartial => Content.IsSummaryPartial;

    public CaptureDetailsContent Content
    {
        get;
        private set => Set(ref field, value);
    } = new();

    public bool IsReading
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(IsReadingDetails));
            }
        }
    }

    public bool IsReadingDetails => IsReading || IsReadingFile;

    private async Task ReadFileAsync()
    {
        if (_disposed || _path == null || _kind == null)
        {
            return;
        }

        if (IsReadingFile)
        {
            _readFileAgain = true;
            return;
        }

        IsReadingFile = true;
        try
        {
            do
            {
                _readFileAgain = false;
                var file = await Task.Run(() => _reader.ReadFileAsync(_path, _kind.Value, _cancellation), _cancellation);
                if (_disposed)
                {
                    return;
                }

                FileProperties = file == null ? new() : CaptureFileProperties.Create(file, _text);
                ReportFileAvailability(file == null);
            }
            while (_readFileAgain && !_disposed);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            ReportFileAvailability(true);
        }
        finally
        {
            IsReadingFile = false;
        }
    }

    private void RefreshContentView()
    {
        Summary = Content.Summary;
        RefreshText();
        RaisePropertyChanged(nameof(HasSummary));
        RaisePropertyChanged(nameof(IsSummaryPartial));
        UpdateActions();
    }

    public async Task RefreshAsync()
    {
        if (_disposed || _path == null)
        {
            return;
        }

        if (IsReading)
        {
            _readAgain = true;
            return;
        }

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
                    RefreshContentView();
                    IsGeneratingSummary = false;
                    IsGeneratingName = false;
                    break;
                }

                CaptureDetailsSnapshot snapshot;
                try
                {
                    // Catalog discovery and projection can be substantial; keep them off the UI thread.
                    snapshot = await Task.Run(() => _reader.ReadAsync(_path, _cancellation), _cancellation);
                    bool sourceMatches = await MatchesWorkingSourceAsync(snapshot);
                    var content = await Task.Run(() => snapshot.Record == null
                        ? new CaptureDetailsContent()
                        : CaptureDetailsContent.Create(snapshot.Record, _text), _cancellation);
                    if (_disposed || revision != Interlocked.Read(ref _revision))
                    {
                        continue;
                    }

                    _sourceMatches = sourceMatches;
                    _snapshot = snapshot;
                    Content = content;
                    RefreshContentView();
                    ReportReadNotice(snapshot);
                    await ReadNameAsync();
                }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    if (_disposed || revision != Interlocked.Read(ref _revision))
                    {
                        continue;
                    }

                    Content = new();
                    _snapshot = null;
                    RefreshContentView();
                    ReportReadNotice(new(CaptureDetailsStatus.Unavailable));
                    IsGeneratingSummary = false;
                    IsGeneratingName = false;
                }
            }
            while (_readAgain && !_disposed);
        }
        finally
        {
            IsReading = false;
        }
    }

    private async Task<bool> MatchesWorkingSourceAsync(CaptureDetailsSnapshot snapshot)
    {
        if (snapshot.Record == null)
        {
            return false;
        }
        if (_workingPath == null || string.Equals(_path, _workingPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return await Task.Run(
            () => _reader.VerifySourceAsync(_workingPath, snapshot.Record.SourceRevision, _cancellation),
            _cancellation);
    }

    private void ReportReadNotice(CaptureDetailsSnapshot snapshot)
    {
        string? state = snapshot.Status switch
        {
            CaptureDetailsStatus.SourceChanged => "SourceChanged",
            CaptureDetailsStatus.SourceUnavailable => "SourceUnavailable",
            CaptureDetailsStatus.Unavailable => "Unavailable",
            _ => null
        };
        if (state != null && state != _readNotice)
        {
            Notify(Text(state), error: state == "Unavailable");
        }

        _readNotice = state;
    }

    private void OnMemoryChanged()
    {
        CaptureMemoryState state = _memory.State;
        // Ignore token/progress fractions; refresh at durable step boundaries and policy/storage changes.
        object key = (state.Activity.CaptureId, state.Activity.Activity, state.Activity.CompletedSteps,
            state.Activity.LastRunStatus, state.Storage, state.IsDeleting, state.Policy.Revision);
        if (Equals(Interlocked.Exchange(ref _stateKey, key), key) || _disposed)
        {
            return;
        }

        Interlocked.Increment(ref _revision);
        _ui.TryExecute(() => ApplyMemoryState(state));
    }

    private void ApplyMemoryState(CaptureMemoryState state)
    {
        if (_disposed)
        {
            return;
        }

        bool revoked = _wasPolicyAllowed && !state.Policy.IsAllowed;
        _wasPolicyAllowed = state.Policy.IsAllowed;
        if ((state.IsDeleting || revoked) && _editorText is { } editor && (editor.Document != null || editor.IsRunning))
        {
            editor.Invalidate(editor.HasChanges);
        }

        if (!state.Policy.IsAllowed)
        {
            IsGeneratingName = false;
            IsGeneratingSummary = false;
        }

        if (state.IsDeleting || state.Storage.HasData == false)
        {
            _snapshot = null;
            Content = new();
            RefreshContentView();
            IsGeneratingName = false;
            IsGeneratingSummary = false;
        }

        _ = RefreshAsync();
    }

    private void ReportFileAvailability(bool unavailable)
    {
        if (unavailable && !_fileUnavailable)
        {
            Notify(_text.GetString("CapturePane_FileUnavailable"), error: true);
        }

        _fileUnavailable = unavailable;
    }
}
