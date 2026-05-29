using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<IVideoFile>
{
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }

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

        IsVideoReady = false;
        IsFinalizingVideo = false;
    }

    public override void Load(IVideoFile video)
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
