using CaptureTool.Application.Implementations.ViewModels.Helpers;
using CaptureTool.Application.Interfaces.UseCases.VideoEdit;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
using CaptureTool.Infrastructure.Implementations.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Telemetry;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<IVideoFile>, IVideoEditPageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = $"LoadVideoEditPage";
        public static readonly string Save = $"Save";
        public static readonly string Copy = $"Copy";
    }

    private const string TelemetryContext = "VideoEditPage";

    public IAsyncAppCommand SaveCommand { get; }
    public IAsyncAppCommand CopyCommand { get; }

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

    private readonly IVideoEditSaveUseCase _saveAction;
    private readonly IVideoEditCopyUseCase _copyAction;
    private readonly ITelemetryService _telemetryService;

    public VideoEditPageViewModel(
        IVideoEditSaveUseCase saveAction,
        IVideoEditCopyUseCase copyAction,
        ITelemetryService telemetryService)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _telemetryService = telemetryService;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        SaveCommand = commandFactory.CreateAsync(ActivityIds.Save, SaveAsync);
        CopyCommand = commandFactory.CreateAsync(ActivityIds.Copy, CopyAsync);

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

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(VideoPath))
        {
            throw new InvalidOperationException("Cannot save video without a valid filepath.");
        }

        await _saveAction.ExecuteCommandAsync(VideoPath, CancellationToken.None);
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(VideoPath))
        {
            throw new InvalidOperationException("Cannot copy video to clipboard without a valid filepath.");
        }

        await _copyAction.ExecuteCommandAsync(VideoPath, CancellationToken.None);
    }
}
