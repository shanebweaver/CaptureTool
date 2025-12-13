using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.ViewModels.Helpers;

namespace CaptureTool.ViewModels;
public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<VideoFile>
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = $"LoadVideoEditPage";
        public static readonly string Save = $"Save";
        public static readonly string Copy = $"Copy";
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand CopyCommand { get; }

    public string? VideoPath
    {
        get => field;
        private set => Set(ref field, value);
    }

    private readonly IVideoEditActions _videoEditActions;
    private readonly ITelemetryService _telemetryService;

    public VideoEditPageViewModel(
        IVideoEditActions videoEditActions,
        ITelemetryService telemetryService)
    {
        _videoEditActions = videoEditActions;
        _telemetryService = telemetryService;

        SaveCommand = new(SaveAsync);
        CopyCommand = new(CopyAsync);
    }

    public override void Load(VideoFile video)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Load, () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

            VideoPath = video.FilePath;

            base.Load(video);
        });
    }

    private Task SaveAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Save, async () =>
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot save video without a valid filepath.");
            }

            await _videoEditActions.SaveAsync(VideoPath, CancellationToken.None);
        });
    }

    private Task CopyAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Copy, async () =>
        {
            if (string.IsNullOrEmpty(VideoPath))
            {
                throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
            }

            await _videoEditActions.CopyAsync(VideoPath, CancellationToken.None);
        });
    }
}
