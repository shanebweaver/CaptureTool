using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Library.CaptureDetails;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.TaskEnvironment;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>
/// Coordinates a capture pane. Analysis, saved content, naming, and editor text
/// each keep their state and operations in a corresponding partial file.
/// </summary>
public sealed partial class CaptureDetailsViewModel : ViewModelBase
{
    private readonly ICaptureMemoryService _memory;
    private readonly IClipboardService _clipboard;
    private readonly ILocalizationService _text;
    private readonly ITaskEnvironment _ui;
    private readonly IAppNotificationService _notifications;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _cancellation;
    private bool _disposed;
    private string? _path;
    private AnalysisMediaKind? _kind;
    private string? _workingPath;
    private readonly IFolderLauncher? _folders;

    public bool HasVisualMedia => _kind is AnalysisMediaKind.Image or AnalysisMediaKind.Video;

    public bool HasAudio => _kind is AnalysisMediaKind.Audio or AnalysisMediaKind.Video;

    public bool IsImage => _kind == AnalysisMediaKind.Image;

    public bool IsVideo => _kind == AnalysisMediaKind.Video;

    public string PhysicalFileName => _path == null ? string.Empty : Path.GetFileName(_path);

    public string FilePath => _path ?? string.Empty;

    public bool IsActive { get; set; } = true;

    public bool HasEdits
    {
        get;
        set
        {
            if (Set(ref field, value) && value && _path != null)
            {
                Notify(_text.GetString("CapturePane_PendingEdits"));
            }
        }
    }

    public IRelayCommand CopyPathCommand { get; }

    public IRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand<string> CopyCommand { get; }

    public CaptureDetailsViewModel(
        ICaptureDetailsReader reader,
        ICaptureMemoryService memory,
        IClipboardService clipboard,
        ILocalizationService text,
        ITaskEnvironment ui,
        IAppNotificationService notifications,
        IFolderLauncher? folders = null,
        ICaptureNamingService? names = null,
        ICaptureAnalysisOnboarding? onboarding = null,
        IAnalysisFeatureAvailability? availability = null)
    {
        _reader = reader;
        _memory = memory;
        _clipboard = clipboard;
        _text = text;
        _ui = ui;
        _wasPolicyAllowed = memory.State.Policy.IsAllowed;
        _notifications = notifications;
        _folders = folders;
        _names = names;
        _onboarding = onboarding;
        _availability = availability;
        AltTextAction = CreateAnalysisAction(AnalysisCapability.ImageAltText, "AltText");
        SummaryAction = CreateAnalysisAction(AnalysisCapability.CaptureSynopsis, "Summary");
        NameAction = CreateAnalysisAction(AnalysisCapability.CaptureName, "Name");
        TextAction = CreateAnalysisAction(AnalysisCapability.TextRecognition, "Text");
        QrAction = CreateAnalysisAction(AnalysisCapability.QrCodeDetection, "Qr");
        ScanTextAction = new(AnalysisCapability.TextRecognition, "CaptureAction_ScanText",
            _text.GetString("CaptureAction_ScanText"),
            () => RequestAnalysisAsync(AnalysisCapability.TextRecognition, scanText: true));
        TranscriptAction = CreateAnalysisAction(AnalysisCapability.Transcription, "Transcript");

        EditNameCommand = new RelayCommand(EditCurrentName);
        CancelNameCommand = new RelayCommand(CancelNameEdit);
        SaveNameCommand = new AsyncRelayCommand(() => RenameAsync(NameDraft));
        SuggestNameCommand = new AsyncRelayCommand(SuggestNameAsync, CanSuggestName);
        if (_names != null)
        {
            _names.Changed += OnNamesChanged;
        }

        TextContent = new(text);
        SpeechContent = new(text);
        VideoText = new(text);
        VideoText.PropertyChanged += VideoTextChanged;
        CopyResultsCommand = new AsyncRelayCommand(() => CopyAsync(DisplayedTextContent.CopyVisibleScope()));
        CopySpeechResultsCommand = new AsyncRelayCommand(() => CopyAsync(SpeechContent.CopyVisibleScope()));
        _cancellation = _lifetime.Token;
        CopyCommand = new AsyncRelayCommand<string>(CopyAsync, value => !_disposed && !string.IsNullOrEmpty(value));
        CopyPathCommand = new RelayCommand(() => _ = CopyPathAsync());
        OpenFolderCommand = new RelayCommand(OpenFolder);
        _memory.StateChanged += OnMemoryChanged;
    }

    public async Task OpenAsync(string path)
    {
        if (_path != null)
        {
            throw new InvalidOperationException("Each details view has its own lifetime.");
        }

        _path = path;
        RaisePropertyChanged(nameof(PhysicalFileName));
        RaisePropertyChanged(nameof(FilePath));
        FileName = Path.GetFileName(path);
        if (HasEdits)
        {
            Notify(_text.GetString("CapturePane_PendingEdits"));
        }

        await Task.WhenAll(RefreshAsync(), ReadNameAsync());
        _kind ??= _snapshot?.Record?.MediaKind;
        await RefreshSupportAsync();
    }

    public Task OpenAsync(string path, AnalysisMediaKind kind, string? workingPath = null)
    {
        _kind = kind;
        UpdateActions();
        _workingPath = workingPath;
        Task analysis = OpenAsync(path);
        return Task.WhenAll(analysis, ReadFileAsync());
    }

    public Task RefreshAllAsync() => Task.WhenAll(RefreshAsync(), ReadFileAsync(), ReadNameAsync(), RefreshSupportAsync());

    private async Task CopyPathAsync()
    {
        if (_disposed || _path == null)
        {
            return;
        }

        try
        {
            await _clipboard.CopyTextAsync(_path);
            if (!_disposed)
            {
                _notifications.ShowInfo(Text("Copied"));
            }
        }
        catch (Exception)
        {
            if (!_disposed)
            {
                _notifications.ShowError(Text("CopyFailed"));
            }
        }
    }

    private void OpenFolder()
    {
        if (_disposed || _path == null)
        {
            return;
        }

        try
        {
            if (Path.GetDirectoryName(_path) is not { } directory || _folders?.TryOpenFolder(directory) != true)
            {
                _notifications.ShowError(_text.GetString("CapturePane_FolderUnavailable"));
            }
        }
        catch (Exception)
        {
            _notifications.ShowError(_text.GetString("CapturePane_FolderUnavailable"));
        }
    }

    public void ReportActionFailure()
    {
        Notify(_text.GetString("CapturePane_ActionFailed"), error: true);
    }

    public void ReportLocationUnavailable() => Notify(_text.GetString("CapturePane_LocationUnavailable"));

    private async Task CopyAsync(string? value)
    {
        if (_disposed || string.IsNullOrEmpty(value) || _memory.State.IsDeleting)
        {
            return;
        }

        try
        {
            await _clipboard.CopyTextAsync(value);
            if (!_disposed)
            {
                _notifications.ShowInfo(Text("Copied"));
            }
        }
        catch (Exception)
        {
            if (!_disposed)
            {
                _notifications.ShowError(Text("CopyFailed"));
            }
        }
    }

    private string Text(string key) => _text.GetString("CaptureDetails_" + key);

    private void Notify(string message, bool error = false)
    {
        if (_disposed || !IsActive)
        {
            return;
        }

        if (error)
        {
            _notifications.ShowError(message);
        }
        else
        {
            _notifications.ShowInfo(message);
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        VideoText.PropertyChanged -= VideoTextChanged;
        if (_editorText != null)
        {
            _editorText.Changed -= EditorTextChanged;
        }

        _memory.StateChanged -= OnMemoryChanged;
        if (_names != null)
        {
            _names.Changed -= OnNamesChanged;
        }

        _lifetime.Cancel();
        _lifetime.Dispose();
        Content = new();
        RefreshContentView();
        FileProperties = new();
        base.Dispose();
    }
}
