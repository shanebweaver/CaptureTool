using CaptureTool.Application.Abstractions.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Abstractions.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Files;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.VideoEdit;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<IVideoFile>
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
    private readonly ITelemetryService _telemetryService;

    public VideoEditPageViewModel(
        ISaveVideoFileUseCase saveAction,
        ICopyVideoFileUseCase copyAction,
        ITelemetryService telemetryService)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _telemetryService = telemetryService;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        ToggleTrimModeCommand = new RelayCommand(ToggleTrimMode);

        IsVideoReady = false;
        IsFinalizingVideo = false;
        IsInTrimMode = false;
        VideoDurationSeconds = 0;
        TrimStartSeconds = 0;
        TrimEndSeconds = 0;
        PlayheadSeconds = 0;
    }

    public override void Load(IVideoFile video)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        VideoPath = video.FilePath;
        ResetTrimRange(0);

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
    }

    public void UpdateTrimStart(double seconds)
    {
        TrimStartSeconds = Math.Clamp(seconds, 0, TrimEndSeconds);
        KeepPlayheadInTrimRange();
        RaisePropertyChanged(nameof(IsTrimmed));
    }

    public void UpdateTrimEnd(double seconds)
    {
        TrimEndSeconds = Math.Clamp(seconds, TrimStartSeconds, VideoDurationSeconds);
        KeepPlayheadInTrimRange();
        RaisePropertyChanged(nameof(IsTrimmed));
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

    private async Task SaveAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot save video without a valid filepath.");
            }

            await _saveAction.ExecuteAsync(
                new SaveVideoFileRequest(VideoPath, GetTrimStartForRequest(), GetTrimEndForRequest()),
                CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(SaveAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(SaveAsync), exception);
        }
    }

    private async Task CopyAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            await _copyAction.ExecuteAsync(
                new CopyVideoFileRequest(VideoPath, GetTrimStartForRequest(), GetTrimEndForRequest()),
                CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(CopyAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(CopyAsync), exception);
        }
    }

    private TimeSpan? GetTrimStartForRequest()
    {
        return IsTrimmed ? TimeSpan.FromSeconds(TrimStartSeconds) : null;
    }

    private TimeSpan? GetTrimEndForRequest()
    {
        return IsTrimmed ? TimeSpan.FromSeconds(TrimEndSeconds) : null;
    }
}
