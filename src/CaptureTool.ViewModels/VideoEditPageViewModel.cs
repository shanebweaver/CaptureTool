using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;

namespace CaptureTool.ViewModels;
public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<IVideoFile>
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = $"LoadVideoEditPage";
        public static readonly string Save = $"Save";
        public static readonly string Copy = $"Copy";
    }

    private const string TelemetryContext = "VideoEditPage";

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }

    public string? VideoPath
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsVideoReady
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsFinalizingVideo
    {
        get => field;
        private set => Set(ref field, value);
    }

    private readonly IVideoEditSaveAction _saveAction;
    private readonly IVideoEditCopyAction _copyAction;
    private readonly ITelemetryService _telemetryService;

    public VideoEditPageViewModel(
        IVideoEditSaveAction saveAction,
        IVideoEditCopyAction copyAction,
        ITelemetryService telemetryService)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _telemetryService = telemetryService;

        SaveCommand = new(SaveAsync);
        CopyCommand = new(CopyAsync);

        IsVideoReady = false;
        IsFinalizingVideo = false;
    }

    public override void Load(IVideoFile video)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, TelemetryContext, ActivityIds.Load, () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

            VideoPath = video.FilePath;

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
        });
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

    private Task SaveAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, TelemetryContext, ActivityIds.Save, async () =>
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot save video without a valid filepath.");
            }

            await _saveAction.ExecuteCommandAsync(VideoPath, CancellationToken.None);
        });
    }

    private Task CopyAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, TelemetryContext, ActivityIds.Copy, async () =>
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            await _copyAction.ExecuteCommandAsync(VideoPath, CancellationToken.None);
        });
    }
}
