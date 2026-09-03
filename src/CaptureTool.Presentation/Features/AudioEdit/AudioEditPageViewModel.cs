using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Application.Abstractions.Edit;
using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Domain.Analysis;
using CaptureTool.Presentation.Features.AnalyzedContent;
using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Features.Media;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Features.AudioEdit;

public sealed partial class AudioEditPageViewModel : LoadableViewModelBase<OpenAudioEditPageRequest>
{
    private const double WaveformMinBarHeight = 0;
    private const double WaveformMaxBarHeight = 132;

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand OpenInClipchampCommand { get; }
    public IAsyncRelayCommand OpenAudioFolderCommand { get; }
    public IRelayCommand RetryMediaCommand { get; }

    public AnalyzedContentViewModel AnalyzedContent { get; }

    public string? AudioPath
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsAudioReady
    {
        get;
        private set => Set(ref field, value);
    }

    public MediaLoadState MediaLoadState
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(IsMediaLoading));
                RaisePropertyChanged(nameof(IsMediaReady));
                RaisePropertyChanged(nameof(HasMediaFailure));
                RaisePropertyChanged(nameof(CanRetryMedia));
                RetryMediaCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public MediaFailureCategory? MediaFailureCategory
    {
        get;
        private set => Set(ref field, value);
    }

    public string MediaFailureMessage
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsMediaLoading => MediaLoadState == MediaLoadState.Loading;
    public bool IsMediaReady => MediaLoadState == MediaLoadState.Ready;
    public bool HasMediaFailure => MediaLoadState == MediaLoadState.Failed;
    public bool CanRetryMedia => HasMediaFailure;

    private readonly ISaveAudioFileUseCase _saveAction;
    private readonly ICopyAudioFileUseCase _copyAction;
    private readonly IOpenExternalEditorUseCase _openExternalEditorAction;
    private readonly IOpenAudioFolderUseCase _openAudioFolderAction;
    private readonly IAudioWaveformHistory _waveformHistory;
    private readonly ILocalizationService? _localizationService;
    private readonly ITelemetryService? _telemetryService;
    private readonly IScratchArtifactStore? _scratchArtifactStore;

    public ObservableCollection<AudioWaveformBarViewModel> WaveformBars
    {
        get;
        private set => Set(ref field, value);
    }

    public AudioEditPageViewModel(
        ISaveAudioFileUseCase saveAction,
        ICopyAudioFileUseCase copyAction,
        IOpenExternalEditorUseCase openExternalEditorAction,
        IOpenAudioFolderUseCase openAudioFolderAction,
        IAudioWaveformHistory waveformHistory,
        ITelemetryService? telemetryService = null,
        ILocalizationService? localizationService = null,
        IScratchArtifactStore? scratchArtifactStore = null,
        AnalyzedContentViewModel? analyzedContent = null)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _openExternalEditorAction = openExternalEditorAction;
        _openAudioFolderAction = openAudioFolderAction;
        _waveformHistory = waveformHistory;
        _telemetryService = telemetryService;
        _localizationService = localizationService;
        _scratchArtifactStore = scratchArtifactStore;
        AnalyzedContent = analyzedContent ?? new AnalyzedContentViewModel();

        SaveCommand = new AsyncRelayCommand(SaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CopyCommand = new AsyncRelayCommand(CopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenInClipchampCommand = new AsyncRelayCommand(OpenInClipchampAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenAudioFolderCommand = new AsyncRelayCommand(OpenAudioFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RetryMediaCommand = new RelayCommand(RetryMedia, () => CanRetryMedia);

        IsAudioReady = false;
        MediaLoadState = MediaLoadState.Loading;
        MediaFailureMessage = string.Empty;
        WaveformBars = [];
        ResetWaveform();
    }

    public override void Dispose()
    {
        if (!string.IsNullOrWhiteSpace(AudioPath))
        {
            _scratchArtifactStore?.DeleteArtifact(AudioPath);
        }

        AudioPath = null;
        AnalyzedContent.Dispose();
        base.Dispose();
    }

    public void Load(AudioFile audio) => Load(new OpenAudioEditPageRequest(audio));

    public override void Load(OpenAudioEditPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfNotReadyToLoad();
        StartLoading();

        AudioFile audio = request.AudioFile;
        AudioPath = audio.FilePath;
        IsAudioReady = true;
        BeginMediaLoading();
        ResetWaveform();

        CaptureEditorContext context = request.EditorContext ?? new CaptureEditorContext(audio.FilePath);
        AnalyzedContent.Load(
            new CaptureMetadataViewRequest(
                CaptureMediaKind.Audio,
                context.CaptureId,
                context.PersistentSourcePath),
            context.InitialMatch);

        base.Load(request);
        TrackEditorOpened();
    }

    public void ReportMediaOpened()
    {
        MediaFailureCategory = null;
        MediaFailureMessage = string.Empty;
        MediaLoadState = MediaLoadState.Ready;
    }

    public void ReportMediaFailed(MediaFailureCategory category)
    {
        MediaFailureCategory = category;
        MediaFailureMessage = GetMediaFailureMessage(category);
        MediaLoadState = MediaLoadState.Failed;
    }

    private void RetryMedia()
    {
        if (!CanRetryMedia)
        {
            return;
        }

        BeginMediaLoading();
    }

    private void BeginMediaLoading()
    {
        MediaFailureCategory = null;
        MediaFailureMessage = string.Empty;
        MediaLoadState = MediaLoadState.Loading;
    }

    private string GetMediaFailureMessage(MediaFailureCategory category)
    {
        string resourceKey = category switch
        {
            global::CaptureTool.Presentation.Features.Media.MediaFailureCategory.FileUnavailable => "MediaFailure_FileUnavailable",
            global::CaptureTool.Presentation.Features.Media.MediaFailureCategory.Unsupported => "MediaFailure_Unsupported",
            _ => "MediaFailure_Playback"
        };

        string? localized = _localizationService?.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(localized) ? resourceKey : localized;
    }

    public IReadOnlyList<double>? GetCapturedWaveformLevels(string audioPath)
        => _waveformHistory.TryGet(audioPath);

    public void SetWaveformLevels(IReadOnlyList<double> levels)
    {
        ObservableCollection<AudioWaveformBarViewModel> bars = [];

        foreach (double level in levels)
        {
            double clampedLevel = Math.Clamp(level, 0, 1);
            double height = GetWaveformBarHeight(clampedLevel);
            bars.Add(new AudioWaveformBarViewModel(height, level: clampedLevel));
        }

        WaveformBars = bars;
    }

    private static double GetWaveformBarHeight(double level)
    {
        double clampedLevel = Math.Clamp(level, 0, 1);
        return WaveformMinBarHeight + (clampedLevel * (WaveformMaxBarHeight - WaveformMinBarHeight));
    }

    private void ResetWaveform()
    {
        WaveformBars = [];
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            return;
        }

        var response = await _saveAction.ExecuteAsync(
            new SaveAudioFileRequest(AudioPath),
            CancellationToken.None);
        TrackOutput(
            "save",
            response?.Result ?? UseCaseResult.Failed,
            response?.Value?.Saved == true);
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            return;
        }

        var response = await _copyAction.ExecuteAsync(
            new CopyAudioFileRequest(AudioPath),
            CancellationToken.None);
        TrackOutput(
            "copy",
            response?.Result ?? UseCaseResult.Failed,
            response?.Value?.Copied == true);
    }

    private async Task OpenAudioFolderAsync()
    {
        await _openAudioFolderAction.ExecuteAsync(new OpenAudioFolderRequest(), CancellationToken.None);
    }

    private async Task OpenInClipchampAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            return;
        }

        var response = await _openExternalEditorAction.ExecuteAsync(
            new OpenExternalEditorRequest(AudioPath, ExternalMediaEditor.Clipchamp),
            CancellationToken.None);
        TrackOutput(
            "open_external_editor",
            response?.Result ?? UseCaseResult.Failed,
            response?.Value?.Opened == true);
    }

    private void TrackEditorOpened()
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.EditorOpened,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "audio"
            });
    }

    private void TrackOutput(string operation, UseCaseResult result, bool completed)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.OutputCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Operation] = operation,
                [TelemetryProperties.MediaType] = "audio",
                [TelemetryProperties.Outcome] = result == UseCaseResult.Cancelled
                    ? TelemetryOutcomes.Canceled
                    : result == UseCaseResult.Succeeded && completed
                        ? TelemetryOutcomes.Succeeded
                        : TelemetryOutcomes.Failed,
                [TelemetryProperties.Source] = "audio_editor"
            });
    }
}
