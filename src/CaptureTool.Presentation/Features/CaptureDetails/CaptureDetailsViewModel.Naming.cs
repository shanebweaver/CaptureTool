using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.CaptureDetails;

public sealed partial class CaptureDetailsViewModel
{
    private readonly ICaptureNamingService? _names;
    private long _nameRevision;
    private bool _acceptedName;
    private bool _reviewSuggestedName;

    public event Action<CaptureFileRename>? FileRenamed;

    public string SuggestedName
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    public bool HasSuggestedName => SuggestedName.Length > 0;

    public string NameDraft
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public bool IsEditingName
    {
        get;
        private set => Set(ref field, value);
    }

    public bool CanEditName => _names != null;

    public IRelayCommand EditNameCommand { get; }

    public IRelayCommand CancelNameCommand { get; }

    public IAsyncRelayCommand SaveNameCommand { get; }

    public IAsyncRelayCommand SuggestNameCommand { get; }

    public string NameSuggestionToolTip => _text.GetString("CaptureNaming_Suggest");

    public string FileName
    {
        get;
        private set => Set(ref field, value);
    } = string.Empty;

    private bool CanSuggestName() => !_disposed && CanEditName &&
        !_memory.State.IsDeleting && !IsGeneratingName &&
        (HasSuggestedName || NameAction.Command.CanExecute(null));

    private void EditCurrentName()
    {
        _reviewSuggestedName = false;
        string name = FileName == PhysicalFileName ? Path.GetFileNameWithoutExtension(FileName) : FileName;
        EditName(name);
    }

    private void CancelNameEdit()
    {
        _reviewSuggestedName = false;
        IsEditingName = false;
    }

    private void EditName(string name)
    {
        NameDraft = name;
        IsEditingName = true;
    }

    private async Task SuggestNameAsync()
    {
        if (HasSuggestedName)
        {
            EditName(SuggestedName);
            return;
        }

        _reviewSuggestedName = true;
        await NameAction.Command.ExecuteAsync(null);
        // A memory update may already be reading the queued run or its result.
        if (!IsReading && !IsGeneratingName)
        {
            _reviewSuggestedName = false;
        }
    }

    private void OnNamesChanged() => _ui.TryExecute(() =>
    {
        if (!_disposed)
        {
            _ = ReadNameAsync();
        }
    });

    private async Task ReadNameAsync()
    {
        if (_names == null || _path == null || _disposed)
        {
            return;
        }

        long revision = Interlocked.Increment(ref _nameRevision);
        var acceptedName = await _names.GetNameAsync(_path, _cancellation);
        if (_disposed || revision != Interlocked.Read(ref _nameRevision))
        {
            return;
        }

        FileName = PhysicalFileName;
        _acceptedName = acceptedName != null;
        SuggestedName = !_acceptedName && Content.SuggestedName.Length > 0
            ? new CaptureName(Content.SuggestedName).SuggestedFileName()
            : string.Empty;
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
        if (_names == null || _path == null || _kind == null || _disposed)
        {
            return;
        }

        try
        {
            _ = new CaptureName(name);
        }
        catch (ArgumentException)
        {
            Notify(_text.GetString("CaptureNaming_Invalid"), error: true);
            return;
        }

        CaptureFileType kind = _kind switch
        {
            AnalysisMediaKind.Image => CaptureFileType.Image,
            AnalysisMediaKind.Audio => CaptureFileType.Audio,
            _ => CaptureFileType.Video
        };
        // Once accepted, finish the filesystem transaction even if the pane closes.
        CaptureFileRename? renamed;
        try
        {
            renamed = await _names.RenameAsync(_path, kind, name, CancellationToken.None);
        }
        catch (Exception)
        {
            Notify(_text.GetString("CaptureNaming_SaveFailed"), error: true);
            return;
        }

        if (renamed == null)
        {
            Notify(_text.GetString("CaptureNaming_SaveFailed"), error: true);
            return;
        }

        if (_workingPath == _path)
        {
            _workingPath = renamed.NewPath;
        }

        _path = renamed.NewPath;
        FileName = PhysicalFileName;
        RaisePropertyChanged(nameof(PhysicalFileName));
        RaisePropertyChanged(nameof(FilePath));
        IsEditingName = false;
        SuggestedName = string.Empty;
        RaisePropertyChanged(nameof(HasSuggestedName));
        FileRenamed?.Invoke(renamed);
        if (!_disposed)
        {
            await RefreshAllAsync();
        }
    }
}
