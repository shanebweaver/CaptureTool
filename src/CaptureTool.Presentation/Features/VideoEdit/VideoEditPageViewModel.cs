using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.VideoEdit;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<IVideoFile>, IEditableSession
{
    private const double TrimComparisonToleranceSeconds = 0.01;

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
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
    private readonly ISettingsService _settingsService;
    private readonly IEditSessionStateStore _editSessionStateStore;
    private readonly ILogService _logService;
    private bool _isRestoringAutosavedTrim;

    public string EditSessionName => "video edit session";

    public bool HasUnsavedChanges
    {
        get;
        private set => Set(ref field, value);
    }

    public VideoEditPageViewModel(
        ISaveVideoFileUseCase saveAction,
        ICopyVideoFileUseCase copyAction,
        ISettingsService settingsService,
        IEditSessionStateStore editSessionStateStore,
        ILogService logService)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _settingsService = settingsService;
        _editSessionStateStore = editSessionStateStore;
        _logService = logService;

        SaveCommand = new AsyncRelayCommand(SaveCommandAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CopyCommand = new AsyncRelayCommand(CopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ToggleTrimModeCommand = new RelayCommand(ToggleTrimMode);

        IsVideoReady = false;
        IsFinalizingVideo = false;
        IsInTrimMode = false;
        VideoDurationSeconds = 0;
        TrimStartSeconds = 0;
        TrimEndSeconds = 0;
        PlayheadSeconds = 0;
        HasUnsavedChanges = false;
    }

    public override void Load(IVideoFile video)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        VideoPath = video.FilePath;
        ResetTrimRange(0);
        HasUnsavedChanges = false;

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

        base.Load(video);
    }

    private async Task WaitForVideoFinalizationAsync(PendingVideoFile pendingVideo)
    {
        try
        {
            await pendingVideo.WhenReadyAsync();
            IsVideoReady = true;
            IsFinalizingVideo = false;
        }
        catch (Exception)
        {
            IsFinalizingVideo = false;
        }
    }

    public void SetVideoDuration(TimeSpan duration)
    {
        double durationSeconds = Math.Max(0, duration.TotalSeconds);
        ResetTrimRange(durationSeconds);
        _ = RestoreAutosavedTrimAsync();
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
    }

    private void ResetTrimRange(double durationSeconds)
    {
        VideoDurationSeconds = durationSeconds;
        TrimStartSeconds = 0;
        TrimEndSeconds = durationSeconds;
        PlayheadSeconds = 0;
        RaisePropertyChanged(nameof(IsTrimmed));
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
        if (response.Value?.Saved == true)
        {
            HasUnsavedChanges = false;
            return true;
        }

        return false;
    }

    public async Task AutoSaveAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(VideoPath) || !_settingsService.Get(CaptureToolSettings.Settings_Edit_AutoSave))
        {
            return;
        }

        try
        {
            await _editSessionStateStore.SaveVideoTrimStateAsync(
                VideoPath,
                new VideoTrimState(VideoDurationSeconds, TrimStartSeconds, TrimEndSeconds),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to auto-save video edit state.");
        }
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

        await _copyAction.ExecuteAsync(
            new CopyVideoFileRequest(VideoPath, GetTrimStartForRequest(), GetTrimEndForRequest()),
            CancellationToken.None);
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
        if (_isRestoringAutosavedTrim)
        {
            return;
        }

        HasUnsavedChanges = IsTrimmed;
        _ = AutoSaveAsync();
    }

    private async Task RestoreAutosavedTrimAsync()
    {
        if (string.IsNullOrEmpty(VideoPath) || VideoDurationSeconds <= 0)
        {
            return;
        }

        try
        {
            VideoTrimState? state = await _editSessionStateStore.TryReadVideoTrimStateAsync(VideoPath);
            if (state is null || Math.Abs(state.DurationSeconds - VideoDurationSeconds) > TrimComparisonToleranceSeconds)
            {
                return;
            }

            _isRestoringAutosavedTrim = true;
            TrimStartSeconds = Math.Clamp(state.TrimStartSeconds, 0, VideoDurationSeconds);
            TrimEndSeconds = Math.Clamp(state.TrimEndSeconds, TrimStartSeconds, VideoDurationSeconds);
            PlayheadSeconds = TrimStartSeconds;
            HasUnsavedChanges = IsTrimmed;
            RaisePropertyChanged(nameof(IsTrimmed));
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to restore video edit state.");
        }
        finally
        {
            _isRestoringAutosavedTrim = false;
        }
    }
}
