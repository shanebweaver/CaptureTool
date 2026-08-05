using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Video.CopyVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.SaveVideoFile;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Ai;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Notifications;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.VideoEdit;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<VideoFile>, IEditableSession
{
    private const double TrimComparisonToleranceSeconds = 0.01;

    private enum VideoVariant
    {
        Original,
        SuperResolution,
    }

    private readonly record struct VideoEditSnapshot(
        double? TrimStartSeconds,
        double? TrimEndSeconds,
        VideoVariant Variant)
    {
        public bool IsEquivalentTo(VideoEditSnapshot other)
        {
            return Variant == other.Variant &&
                AreEquivalent(TrimStartSeconds, other.TrimStartSeconds) &&
                AreEquivalent(TrimEndSeconds, other.TrimEndSeconds);
        }

        private static bool AreEquivalent(double? first, double? second)
        {
            if (!first.HasValue || !second.HasValue)
            {
                return first.HasValue == second.HasValue;
            }

            return Math.Abs(first.Value - second.Value) <= TrimComparisonToleranceSeconds;
        }
    }

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand EditInClipchampCommand { get; }
    public IAsyncRelayCommand OpenVideosFolderCommand { get; }
    public IAsyncRelayCommand ToggleVideoSuperResolutionCommand { get; }
    public IRelayCommand ToggleTrimModeCommand { get; }

    public string? VideoPath
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsVideoReady
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsFinalizingVideo
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsVideoSuperResolutionFeatureEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsVideoSuperResolutionAvailable
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsVideoSuperResolutionActive
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsVideoSuperResolutionGenerating
    {
        get;
        private set => Set(ref field, value);
    }

    public string VideoSuperResolutionStatusMessage
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsInTrimMode
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(AreTransportControlsVisible));
            }
        }
    }

    public bool AreTransportControlsVisible => !IsInTrimMode;

    public double VideoDurationSeconds
    {
        get;
        private set => Set(ref field, value);
    }

    public double TrimStartSeconds
    {
        get;
        private set => Set(ref field, value);
    }

    public double TrimEndSeconds
    {
        get;
        private set => Set(ref field, value);
    }

    public double PlayheadSeconds
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsTrimmed => VideoDurationSeconds > 0 &&
        (TrimStartSeconds > TrimComparisonToleranceSeconds ||
            TrimEndSeconds < VideoDurationSeconds - TrimComparisonToleranceSeconds);

    private readonly ISaveVideoFileUseCase _saveAction;
    private readonly ICopyVideoFileUseCase _copyAction;
    private readonly IOpenExternalEditorUseCase _openExternalEditorAction;
    private readonly IOpenVideosFolderUseCase _openVideosFolderAction;
    private readonly ILogService _logService;
    private readonly IVideoSuperResolutionService _videoSuperResolutionService;
    private readonly IVideoSuperResolutionFeatureAvailability _videoSuperResolutionFeatureAvailability;
    private readonly IAiFeatureConsentService _aiFeatureConsentService;
    private readonly IAiFeatureConsentDialogService _aiFeatureConsentDialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppNotificationService _notificationService;
    private readonly ITelemetryService? _telemetryService;
    private readonly IScratchArtifactStore? _scratchArtifactStore;
    private readonly HashSet<string> _scratchArtifactPaths = new(StringComparer.OrdinalIgnoreCase);
    private string? _originalVideoPath;
    private string? _superResolutionVideoPath;
    private VideoEditSnapshot? _savedEditSnapshot;

    public string EditSessionName => "video edit session";

    public bool HasUnsavedChanges
    {
        get;
        private set => Set(ref field, value);
    }

    public VideoEditPageViewModel(
        ISaveVideoFileUseCase saveAction,
        ICopyVideoFileUseCase copyAction,
        IOpenExternalEditorUseCase openExternalEditorAction,
        IOpenVideosFolderUseCase openVideosFolderAction,
        ILogService logService,
        IVideoSuperResolutionService? videoSuperResolutionService = null,
        IVideoSuperResolutionFeatureAvailability? videoSuperResolutionFeatureAvailability = null,
        IAiFeatureConsentService? aiFeatureConsentService = null,
        IAiFeatureConsentDialogService? aiFeatureConsentDialogService = null,
        ILocalizationService? localizationService = null,
        IAppNotificationService? notificationService = null,
        ITelemetryService? telemetryService = null,
        IScratchArtifactStore? scratchArtifactStore = null)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _openExternalEditorAction = openExternalEditorAction;
        _openVideosFolderAction = openVideosFolderAction;
        _logService = logService;
        _videoSuperResolutionService = videoSuperResolutionService ?? new UnsupportedVideoSuperResolutionService();
        _videoSuperResolutionFeatureAvailability =
            videoSuperResolutionFeatureAvailability ?? new DisabledVideoSuperResolutionFeatureAvailability();
        _aiFeatureConsentService = aiFeatureConsentService ?? new PermissiveAiFeatureConsentService();
        _aiFeatureConsentDialogService = aiFeatureConsentDialogService ?? new PermissiveAiFeatureConsentDialogService();
        _localizationService = localizationService ?? new ResourceKeyLocalizationService();
        _notificationService = notificationService ?? new NullAppNotificationService();
        _telemetryService = telemetryService;
        _scratchArtifactStore = scratchArtifactStore;

        SaveCommand = new AsyncRelayCommand(SaveCommandAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CopyCommand = new AsyncRelayCommand(CopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        EditInClipchampCommand = new AsyncRelayCommand(EditInClipchampAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenVideosFolderCommand = new AsyncRelayCommand(OpenVideosFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleVideoSuperResolutionCommand = new AsyncRelayCommand(
            ToggleVideoSuperResolutionAsync,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleTrimModeCommand = new RelayCommand(ToggleTrimMode);

        IsVideoReady = false;
        IsFinalizingVideo = false;
        IsInTrimMode = false;
        VideoDurationSeconds = 0;
        TrimStartSeconds = 0;
        TrimEndSeconds = 0;
        PlayheadSeconds = 0;
        HasUnsavedChanges = false;
        VideoSuperResolutionStatusMessage = string.Empty;
    }

    public override void Load(VideoFile video)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        _originalVideoPath = video.FilePath;
        TrackScratchArtifact(video.FilePath);
        _superResolutionVideoPath = null;
        _savedEditSnapshot = null;
        VideoPath = _originalVideoPath;
        IsVideoSuperResolutionActive = false;
        IsVideoSuperResolutionGenerating = false;
        VideoSuperResolutionStatusMessage = string.Empty;
        IsVideoSuperResolutionFeatureEnabled =
            _videoSuperResolutionFeatureAvailability.IsVideoSuperResolutionEnabled;
        ResetTrimRange(0);
        _savedEditSnapshot = CaptureEditSnapshot();
        UpdateHasUnsavedChanges();

        if (video is PendingVideoFile pendingVideo)
        {
            IsVideoReady = false;
            IsFinalizingVideo = true;
            _ = WaitForVideoFinalizationAsync(pendingVideo);
        }
        else
        {
            IsVideoReady = true;
            IsFinalizingVideo = false;
        }

        UpdateVideoSuperResolutionAvailability();
        base.Load(video);
        TrackEditorOpened();
    }

    private async Task WaitForVideoFinalizationAsync(PendingVideoFile pendingVideo)
    {
        try
        {
            await pendingVideo.WhenReadyAsync();
            IsVideoReady = true;
            IsFinalizingVideo = false;
            UpdateVideoSuperResolutionAvailability();
        }
        catch (Exception)
        {
            IsFinalizingVideo = false;
            UpdateVideoSuperResolutionAvailability();
        }
    }

    public void SetVideoDuration(TimeSpan duration)
    {
        double durationSeconds = Math.Max(0, duration.TotalSeconds);
        if (VideoDurationSeconds > 0 &&
            Math.Abs(VideoDurationSeconds - durationSeconds) <= TrimComparisonToleranceSeconds)
        {
            return;
        }

        ResetTrimRange(durationSeconds);
    }

    public void UpdateTrimStart(double seconds)
    {
        TrimStartSeconds = Math.Clamp(seconds, 0, TrimEndSeconds);
        KeepPlayheadInTrimRange();
        RaisePropertyChanged(nameof(IsTrimmed));
        OnTrimChanged();
    }

    public void UpdateTrimEnd(double seconds)
    {
        TrimEndSeconds = Math.Clamp(seconds, TrimStartSeconds, VideoDurationSeconds);
        KeepPlayheadInTrimRange();
        RaisePropertyChanged(nameof(IsTrimmed));
        OnTrimChanged();
    }

    public void UpdatePlayhead(double seconds)
    {
        PlayheadSeconds = ClampToTrimRange(seconds);
    }

    private void ToggleTrimMode()
    {
        IsInTrimMode = !IsInTrimMode;
        KeepPlayheadInTrimRange();
        TrackEditTool("trim_mode", TelemetryOutcomes.Succeeded, IsInTrimMode);
    }

    private void ResetTrimRange(double durationSeconds)
    {
        VideoDurationSeconds = durationSeconds;
        TrimStartSeconds = 0;
        TrimEndSeconds = durationSeconds;
        PlayheadSeconds = 0;
        RaisePropertyChanged(nameof(IsTrimmed));
        UpdateHasUnsavedChanges();
    }

    private void KeepPlayheadInTrimRange()
    {
        PlayheadSeconds = ClampToTrimRange(PlayheadSeconds);
    }

    private double ClampToTrimRange(double seconds)
    {
        return Math.Clamp(seconds, TrimStartSeconds, TrimEndSeconds);
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(VideoPath))
        {
            return false;
        }

        var response = await _saveAction.ExecuteAsync(
            new SaveVideoFileRequest(VideoPath, GetTrimStartForRequest(), GetTrimEndForRequest()),
            cancellationToken);
        bool saved = response?.Value?.Saved == true;
        TrackOutput("save", response?.Result ?? UseCaseResult.Failed, saved);
        if (saved)
        {
            if (IsTrimmed)
            {
                TrackEditTool("trim", TelemetryOutcomes.Succeeded);
            }

            _savedEditSnapshot = CaptureEditSnapshot();
            UpdateHasUnsavedChanges();
            return true;
        }

        return false;
    }

    private async Task SaveCommandAsync()
    {
        await SaveAsync(CancellationToken.None);
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(VideoPath))
        {
            return;
        }

        var response = await _copyAction.ExecuteAsync(
            new CopyVideoFileRequest(VideoPath, GetTrimStartForRequest(), GetTrimEndForRequest()),
            CancellationToken.None);
        TrackOutput(
            "copy",
            response?.Result ?? UseCaseResult.Failed,
            response?.Value?.Copied == true);
    }

    private async Task OpenVideosFolderAsync()
    {
        await _openVideosFolderAction.ExecuteAsync(new OpenVideosFolderRequest(), CancellationToken.None);
    }

    private async Task ToggleVideoSuperResolutionAsync()
    {
        if (IsVideoSuperResolutionActive)
        {
            ShowOriginalVideo();
            return;
        }

        if (!IsVideoSuperResolutionAvailable || string.IsNullOrWhiteSpace(_originalVideoPath))
        {
            return;
        }

        if (!await EnsureAiFeatureConsentAsync(
            AiFeatureId.VideoSuperResolution,
            CancellationToken.None))
        {
            TrackEditTool("super_resolution", TelemetryOutcomes.Canceled);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_superResolutionVideoPath) &&
            File.Exists(_superResolutionVideoPath))
        {
            ShowSuperResolutionVideo(_superResolutionVideoPath);
            return;
        }

        VideoSuperResolutionStatusMessage = string.Empty;
        IsVideoSuperResolutionGenerating = true;
        UpdateVideoSuperResolutionAvailability();
        try
        {
            VideoSuperResolutionReadyState readyState = _videoSuperResolutionService.GetReadyState();
            if (readyState == VideoSuperResolutionReadyState.PreparationNeeded)
            {
                VideoSuperResolutionPreparationResult preparationResult =
                    await _videoSuperResolutionService.EnsureReadyAsync(CancellationToken.None);
                if (preparationResult.Status != VideoSuperResolutionPreparationStatus.Success)
                {
                    ShowVideoSuperResolutionFailure(GetPreparationFailureMessage(preparationResult));
                    return;
                }
            }
            else if (readyState != VideoSuperResolutionReadyState.Ready)
            {
                ShowVideoSuperResolutionFailure(GetReadyStateFailureMessage(readyState));
                return;
            }

            VideoSuperResolutionResult result = await _videoSuperResolutionService.GenerateAsync(
                new VideoSuperResolutionRequest(new VideoFile(_originalVideoPath)),
                CancellationToken.None);
            if (result.Status != VideoSuperResolutionStatus.Success ||
                result.VideoFile is null)
            {
                ShowVideoSuperResolutionFailure(GetGenerationFailureMessage(result));
                return;
            }

            _superResolutionVideoPath = result.VideoFile.FilePath;
            TrackScratchArtifact(_superResolutionVideoPath);
            ShowSuperResolutionVideo(_superResolutionVideoPath);
        }
        catch (OperationCanceledException)
        {
            VideoSuperResolutionStatusMessage = string.Empty;
            TrackEditTool("super_resolution", TelemetryOutcomes.Canceled);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to generate super-resolution video.");
            ShowVideoSuperResolutionFailure(
                GetLocalizedString("VideoSuperResolutionStatus_Failed"));
        }
        finally
        {
            IsVideoSuperResolutionGenerating = false;
            UpdateVideoSuperResolutionAvailability();
        }
    }

    private async Task<bool> EnsureAiFeatureConsentAsync(
        AiFeatureId featureId,
        CancellationToken cancellationToken)
    {
        if (_aiFeatureConsentService.GetConsentState(featureId) == AiFeatureConsentState.Granted)
        {
            return true;
        }

        bool consented = await _aiFeatureConsentDialogService.RequestConsentAsync(
            featureId,
            cancellationToken);
        await _aiFeatureConsentService.SetConsentAsync(
            featureId,
            consented,
            cancellationToken);
        UpdateVideoSuperResolutionAvailability();
        return consented;
    }

    private void ShowOriginalVideo()
    {
        if (string.IsNullOrWhiteSpace(_originalVideoPath))
        {
            return;
        }

        VideoPath = _originalVideoPath;
        IsVideoSuperResolutionActive = false;
        UpdateHasUnsavedChanges();
        UpdateVideoSuperResolutionAvailability();
    }

    private void ShowSuperResolutionVideo(string videoPath)
    {
        VideoPath = videoPath;
        IsVideoSuperResolutionActive = true;
        VideoSuperResolutionStatusMessage = string.Empty;
        UpdateHasUnsavedChanges();
        UpdateVideoSuperResolutionAvailability();
        TrackEditTool("super_resolution", TelemetryOutcomes.Succeeded);
    }

    public override void Dispose()
    {
        foreach (string artifactPath in _scratchArtifactPaths)
        {
            _scratchArtifactStore?.DeleteArtifact(artifactPath);
        }
        _scratchArtifactPaths.Clear();
        _originalVideoPath = null;
        _superResolutionVideoPath = null;
        VideoPath = null;
        base.Dispose();
    }

    private void TrackScratchArtifact(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return;
        }

        if (LoadState == CaptureTool.Presentation.Loading.LoadState.Disposed)
        {
            _scratchArtifactStore?.DeleteArtifact(artifactPath);
        }
        else
        {
            _scratchArtifactPaths.Add(artifactPath);
        }
    }

    private void UpdateVideoSuperResolutionAvailability()
    {
        IsVideoSuperResolutionFeatureEnabled =
            _videoSuperResolutionFeatureAvailability.IsVideoSuperResolutionEnabled;
        VideoSuperResolutionReadyState readyState = _videoSuperResolutionService.GetReadyState();
        IsVideoSuperResolutionAvailable =
            IsVideoSuperResolutionFeatureEnabled &&
            IsVideoReady &&
            !IsFinalizingVideo &&
            !IsVideoSuperResolutionGenerating &&
            (readyState is VideoSuperResolutionReadyState.Ready or
                VideoSuperResolutionReadyState.PreparationNeeded) &&
            (IsVideoSuperResolutionActive ||
                _aiFeatureConsentService.GetConsentState(AiFeatureId.VideoSuperResolution) !=
                    AiFeatureConsentState.Denied);
    }

    private string GetReadyStateFailureMessage(VideoSuperResolutionReadyState readyState)
    {
        return readyState switch
        {
            VideoSuperResolutionReadyState.NotSupported =>
                GetLocalizedString("VideoSuperResolutionStatus_NotSupported"),
            VideoSuperResolutionReadyState.Disabled =>
                GetLocalizedString("VideoSuperResolutionStatus_Disabled"),
            _ => GetLocalizedString("VideoSuperResolutionStatus_NotAvailable")
        };
    }

    private string GetPreparationFailureMessage(VideoSuperResolutionPreparationResult result)
    {
        return result.Status switch
        {
            VideoSuperResolutionPreparationStatus.Cancelled => string.Empty,
            VideoSuperResolutionPreparationStatus.NotSupported =>
                GetLocalizedString("VideoSuperResolutionStatus_NotSupported"),
            VideoSuperResolutionPreparationStatus.Failed =>
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? GetLocalizedString("VideoSuperResolutionStatus_PreparationFailed")
                    : result.ErrorMessage,
            _ => GetLocalizedString("VideoSuperResolutionStatus_PreparationFailed")
        };
    }

    private string GetGenerationFailureMessage(VideoSuperResolutionResult result)
    {
        return result.Status switch
        {
            VideoSuperResolutionStatus.Cancelled => string.Empty,
            VideoSuperResolutionStatus.NotReady =>
                GetLocalizedString("VideoSuperResolutionStatus_NotReady"),
            VideoSuperResolutionStatus.NotSupported =>
                GetLocalizedString("VideoSuperResolutionStatus_NotSupported"),
            VideoSuperResolutionStatus.UnsupportedVideo =>
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? GetLocalizedString("VideoSuperResolutionStatus_UnsupportedVideo")
                    : result.ErrorMessage,
            VideoSuperResolutionStatus.Failed =>
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? GetLocalizedString("VideoSuperResolutionStatus_Failed")
                    : result.ErrorMessage,
            _ => GetLocalizedString("VideoSuperResolutionStatus_Failed")
        };
    }

    private void ShowVideoSuperResolutionFailure(string message)
    {
        VideoSuperResolutionStatusMessage = message;
        TrackEditTool(
            "super_resolution",
            string.IsNullOrWhiteSpace(message)
                ? TelemetryOutcomes.Canceled
                : TelemetryOutcomes.Failed);
        if (!string.IsNullOrWhiteSpace(message))
        {
            _notificationService.ShowError(message);
        }
    }

    private string GetLocalizedString(string resourceKey)
    {
        string value = _localizationService.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(value) ? resourceKey : value;
    }

    private async Task EditInClipchampAsync()
    {
        if (string.IsNullOrEmpty(VideoPath))
        {
            return;
        }

        var response = await _openExternalEditorAction.ExecuteAsync(
            new OpenExternalEditorRequest(VideoPath, ExternalMediaEditor.Clipchamp),
            CancellationToken.None);
        TrackOutput(
            "open_external_editor",
            response?.Result ?? UseCaseResult.Failed,
            response?.Value?.Opened == true);
    }

    private TimeSpan? GetTrimStartForRequest()
    {
        return IsTrimmed ? TimeSpan.FromSeconds(TrimStartSeconds) : null;
    }

    private TimeSpan? GetTrimEndForRequest()
    {
        return IsTrimmed ? TimeSpan.FromSeconds(TrimEndSeconds) : null;
    }

    private void OnTrimChanged()
    {
        UpdateHasUnsavedChanges();
    }

    private VideoEditSnapshot CaptureEditSnapshot()
    {
        return new VideoEditSnapshot(
            IsTrimmed ? TrimStartSeconds : null,
            IsTrimmed ? TrimEndSeconds : null,
            IsVideoSuperResolutionActive
                ? VideoVariant.SuperResolution
                : VideoVariant.Original);
    }

    private void UpdateHasUnsavedChanges()
    {
        HasUnsavedChanges = _savedEditSnapshot.HasValue &&
            !_savedEditSnapshot.Value.IsEquivalentTo(CaptureEditSnapshot());
    }

    private void TrackEditorOpened()
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.EditorOpened,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.MediaType] = "video"
            });
    }

    private void TrackEditTool(string tool, string outcome, bool? enabled = null)
    {
        Dictionary<string, object?> properties = new()
        {
            [TelemetryProperties.Tool] = tool,
            [TelemetryProperties.MediaType] = "video",
            [TelemetryProperties.Outcome] = outcome
        };

        if (enabled.HasValue)
        {
            properties[TelemetryProperties.Enabled] = enabled.Value;
        }

        _telemetryService?.TrackEvent(TelemetryEvents.EditToolInvoked, properties);
    }

    private void TrackOutput(string operation, UseCaseResult result, bool completed)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.OutputCompleted,
            new Dictionary<string, object?>
            {
                [TelemetryProperties.Operation] = operation,
                [TelemetryProperties.MediaType] = "video",
                [TelemetryProperties.Outcome] = result == UseCaseResult.Cancelled
                    ? TelemetryOutcomes.Canceled
                    : result == UseCaseResult.Succeeded && completed
                        ? TelemetryOutcomes.Succeeded
                        : TelemetryOutcomes.Failed,
                [TelemetryProperties.Source] = "video_editor"
            });
    }

    private sealed class DisabledVideoSuperResolutionFeatureAvailability :
        IVideoSuperResolutionFeatureAvailability
    {
        public bool IsVideoSuperResolutionEnabled => false;
    }

    private sealed class UnsupportedVideoSuperResolutionService : IVideoSuperResolutionService
    {
        public VideoSuperResolutionReadyState GetReadyState()
        {
            return VideoSuperResolutionReadyState.NotSupported;
        }

        public Task<VideoSuperResolutionPreparationResult> EnsureReadyAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VideoSuperResolutionPreparationResult.NotSupported);
        }

        public Task<VideoSuperResolutionResult> GenerateAsync(
            VideoSuperResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(VideoSuperResolutionResult.NotSupported);
        }
    }

    private sealed class PermissiveAiFeatureConsentService : IAiFeatureConsentService
    {
        public IReadOnlyList<AiFeatureConsent> GetFeatureConsents()
        {
            return [];
        }

        public AiFeatureConsentState GetConsentState(AiFeatureId featureId)
        {
            return AiFeatureConsentState.Granted;
        }

        public Task SetConsentAsync(
            AiFeatureId featureId,
            bool isGranted,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class PermissiveAiFeatureConsentDialogService : IAiFeatureConsentDialogService
    {
        public Task<bool> RequestConsentAsync(
            AiFeatureId featureId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class ResourceKeyLocalizationService : ILocalizationService
    {
        public IAppLanguage? LanguageOverride => null;
        public IAppLanguage? RequestedLanguage => null;
        public IAppLanguage? StartupLanguage => null;
        public IAppLanguage? DefaultLanguage => null;
        public IAppLanguage[] SupportedLanguages => [];

        public void Initialize(string languageOverride)
        {
        }

        public string GetString(string resourceKey)
        {
            return resourceKey;
        }

        public void OverrideLanguage(IAppLanguage? language)
        {
        }
    }

    private sealed class NullAppNotificationService : IAppNotificationService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public AppNotification? CurrentNotification => null;
        public bool HasNotification => false;
        public int NotificationCount => 0;

        public void ShowError(string message)
        {
        }

        public void ShowInfo(string message)
        {
        }

        public void DismissCurrent()
        {
        }
    }
}
