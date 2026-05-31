using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.VideoEdit;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<IVideoFile>
{
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
        private set => Set(ref field, value);
    }

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

    private readonly IUseCase<SaveVideoFileRequest, SaveVideoFileResponse> _saveAction;
    private readonly IUseCase<CopyVideoFileRequest, CopyVideoFileResponse> _copyAction;
    private readonly ITelemetryService _telemetryService;

    public VideoEditPageViewModel(
        IUseCase<SaveVideoFileRequest, SaveVideoFileResponse> saveAction,
        IUseCase<CopyVideoFileRequest, CopyVideoFileResponse> copyAction,
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
    }

    public void UpdateTrimEnd(double seconds)
    {
        TrimEndSeconds = Math.Clamp(seconds, TrimStartSeconds, VideoDurationSeconds);
        KeepPlayheadInTrimRange();
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
        if (string.IsNullOrEmpty(VideoPath))
        {
            throw new InvalidOperationException("Cannot save video without a valid filepath.");
        }

        await _saveAction.ExecuteAsync(new SaveVideoFileRequest(VideoPath), CancellationToken.None);
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(VideoPath))
        {
            throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
        }

        await _copyAction.ExecuteAsync(new CopyVideoFileRequest(VideoPath), CancellationToken.None);
    }
}
