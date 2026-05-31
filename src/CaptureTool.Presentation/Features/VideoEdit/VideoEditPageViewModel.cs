using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoEdit.CopyVideoFile;
using CaptureTool.Application.Features.VideoEdit.SaveVideoFile;
using CaptureTool.Application.Features.VideoEdit.ScanVideoMetadata;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.TaskEnvironment;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.VideoEdit;

public sealed partial class VideoEditPageViewModel : LoadableViewModelBase<IVideoFile>
{
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IAsyncRelayCommand ScanMetadataCommand { get; }

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

    public bool IsScanningMetadata
    {
        get;
        private set => Set(ref field, value);
    }

    public bool CanScanMetadata
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsMetadataScanningFeatureEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public bool HasMetadataScanStatus
    {
        get;
        private set => Set(ref field, value);
    }

    public double MetadataScanProgress
    {
        get;
        private set => Set(ref field, value);
    }

    public string? MetadataScanStatus
    {
        get;
        private set => Set(ref field, value);
    }

    private readonly IUseCase<SaveVideoFileRequest, SaveVideoFileResponse> _saveAction;
    private readonly IUseCase<CopyVideoFileRequest, CopyVideoFileResponse> _copyAction;
    private readonly IUseCase<ScanVideoMetadataRequest, ScanVideoMetadataResponse> _scanMetadataAction;
    private readonly IFeatureManager _featureManager;
    private readonly ITaskEnvironment _taskEnvironment;
    private readonly ITelemetryService _telemetryService;
    private IMetadataScanJob? _metadataScanJob;

    public VideoEditPageViewModel(
        IUseCase<SaveVideoFileRequest, SaveVideoFileResponse> saveAction,
        IUseCase<CopyVideoFileRequest, CopyVideoFileResponse> copyAction,
        IUseCase<ScanVideoMetadataRequest, ScanVideoMetadataResponse> scanMetadataAction,
        IFeatureManager featureManager,
        ITaskEnvironment taskEnvironment,
        ITelemetryService telemetryService)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _scanMetadataAction = scanMetadataAction;
        _featureManager = featureManager;
        _taskEnvironment = taskEnvironment;
        _telemetryService = telemetryService;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);
        ScanMetadataCommand = new AsyncRelayCommand(ScanMetadataAsync);

        IsVideoReady = false;
        IsFinalizingVideo = false;
        IsScanningMetadata = false;
        IsMetadataScanningFeatureEnabled = false;
        CanScanMetadata = false;
        HasMetadataScanStatus = false;
    }

    public override void Load(IVideoFile video)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        UnsubscribeFromMetadataScanJob();
        HasMetadataScanStatus = false;
        MetadataScanStatus = null;
        MetadataScanProgress = 0;
        IsMetadataScanningFeatureEnabled = _featureManager.IsEnabled(AppFeatures.Feature_VideoCapture_MetadataCollection);
        VideoPath = video.FilePath;

        if (video is PendingVideoFile pendingVideo)
        {
            IsVideoReady = false;
            IsFinalizingVideo = true;
            CanScanMetadata = false;
            _ = WaitForVideoFinalizationAsync(pendingVideo);
        }
        else
        {
            IsVideoReady = true;
            IsFinalizingVideo = false;
            CanScanMetadata = IsMetadataScanningFeatureEnabled;
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
            CanScanMetadata = IsMetadataScanningFeatureEnabled;
        }
        catch (Exception)
        {
            IsFinalizingVideo = false;
            CanScanMetadata = false;
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

    private async Task ScanMetadataAsync()
    {
        if (string.IsNullOrEmpty(VideoPath))
        {
            throw new InvalidOperationException("Cannot scan video metadata without a valid filepath.");
        }

        if (!IsMetadataScanningFeatureEnabled)
        {
            throw new InvalidOperationException("Cannot scan video metadata when metadata collection is disabled.");
        }

        UnsubscribeFromMetadataScanJob();
        MetadataScanProgress = 0;
        MetadataScanStatus = "Queued metadata scan";
        HasMetadataScanStatus = true;
        IsScanningMetadata = true;
        CanScanMetadata = false;

        ScanVideoMetadataResponse response = await _scanMetadataAction.ExecuteAsync(new ScanVideoMetadataRequest(VideoPath), CancellationToken.None);
        _metadataScanJob = response.ScanJob;
        _metadataScanJob.StatusChanged += MetadataScanJob_StatusChanged;
        _metadataScanJob.ProgressChanged += MetadataScanJob_ProgressChanged;

        MetadataScanProgress = _metadataScanJob.Progress;
        UpdateMetadataScanStatus(_metadataScanJob.Status);
    }

    private void MetadataScanJob_StatusChanged(object? sender, MetadataScanJobStatus status)
    {
        _taskEnvironment.TryExecute(() => UpdateMetadataScanStatus(status));
    }

    private void MetadataScanJob_ProgressChanged(object? sender, double progress)
    {
        _taskEnvironment.TryExecute(() => MetadataScanProgress = progress);
    }

    private void UpdateMetadataScanStatus(MetadataScanJobStatus status)
    {
        MetadataScanStatus = status switch
        {
            MetadataScanJobStatus.Queued => "Queued metadata scan",
            MetadataScanJobStatus.Processing => "Scanning metadata",
            MetadataScanJobStatus.Completed => $"Metadata saved: {_metadataScanJob?.MetadataFilePath}",
            MetadataScanJobStatus.Failed => $"Metadata scan failed: {_metadataScanJob?.ErrorMessage}",
            MetadataScanJobStatus.Cancelled => "Metadata scan cancelled",
            _ => null
        };

        IsScanningMetadata = status is MetadataScanJobStatus.Queued or MetadataScanJobStatus.Processing;
        CanScanMetadata = IsMetadataScanningFeatureEnabled && IsVideoReady && !IsScanningMetadata;
        HasMetadataScanStatus = MetadataScanStatus is not null;
    }

    private void UnsubscribeFromMetadataScanJob()
    {
        if (_metadataScanJob is null)
        {
            return;
        }

        _metadataScanJob.StatusChanged -= MetadataScanJob_StatusChanged;
        _metadataScanJob.ProgressChanged -= MetadataScanJob_ProgressChanged;
        _metadataScanJob = null;
    }
}
