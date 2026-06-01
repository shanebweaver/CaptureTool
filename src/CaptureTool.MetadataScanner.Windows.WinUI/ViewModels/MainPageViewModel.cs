using CaptureTool.MetadataScanner.Windows.WinUI.Metadata;
using CaptureTool.MetadataScanner.Windows.WinUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Storage;

namespace CaptureTool.MetadataScanner.Windows.WinUI.ViewModels;

public sealed partial class MainPageViewModel(
    IMediaFilePicker mediaFilePicker,
    IMetadataScanningService metadataScanningService) : ObservableObject
{
    private readonly DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private MediaSource? mediaSource;
    private string selectedFileName = "No media file selected";
    private string? selectedFilePath;
    private IMetadataScanJob? currentScanJob;
    private string scanStatus = "Select a media file to scan.";
    private double scanProgress;
    private string? metadataFilePath;

    public MediaSource? MediaSource
    {
        get => mediaSource;
        set => SetProperty(ref mediaSource, value);
    }

    public string SelectedFileName
    {
        get => selectedFileName;
        set => SetProperty(ref selectedFileName, value);
    }

    public string? SelectedFilePath
    {
        get => selectedFilePath;
        private set
        {
            if (SetProperty(ref selectedFilePath, value))
            {
                ScanMetadataCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ScanStatus
    {
        get => scanStatus;
        private set => SetProperty(ref scanStatus, value);
    }

    public double ScanProgress
    {
        get => scanProgress;
        private set => SetProperty(ref scanProgress, value);
    }

    public string? MetadataFilePath
    {
        get => metadataFilePath;
        private set => SetProperty(ref metadataFilePath, value);
    }

    [RelayCommand]
    private async Task OpenMediaFileAsync()
    {
        StorageFile? mediaFile = await mediaFilePicker.PickMediaFileAsync();
        if (mediaFile is null)
        {
            return;
        }

        MediaSource = MediaSource.CreateFromStorageFile(mediaFile);
        SelectedFileName = mediaFile.Name;
        SelectedFilePath = mediaFile.Path;
        MetadataFilePath = null;
        ScanProgress = 0;
        ScanStatus = "Ready to scan metadata.";
    }

    [RelayCommand(CanExecute = nameof(CanScanMetadata))]
    private void ScanMetadata()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            return;
        }

        DetachCurrentScanJob();

        currentScanJob = metadataScanningService.QueueScan(SelectedFilePath);
        currentScanJob.StatusChanged += OnScanJobStatusChanged;
        currentScanJob.ProgressChanged += OnScanJobProgressChanged;

        ScanProgress = currentScanJob.Progress;
        UpdateScanStatus(currentScanJob);
    }

    private bool CanScanMetadata()
    {
        return !string.IsNullOrWhiteSpace(SelectedFilePath) &&
            currentScanJob?.Status is not MetadataScanJobStatus.Queued and not MetadataScanJobStatus.Processing;
    }

    private void OnScanJobStatusChanged(object? sender, MetadataScanJobStatus status)
    {
        if (sender is not IMetadataScanJob job)
        {
            return;
        }

        dispatcherQueue.TryEnqueue(() =>
        {
            UpdateScanStatus(job);
            ScanMetadataCommand.NotifyCanExecuteChanged();
        });
    }

    private void OnScanJobProgressChanged(object? sender, double progress)
    {
        dispatcherQueue.TryEnqueue(() => ScanProgress = progress);
    }

    private void UpdateScanStatus(IMetadataScanJob job)
    {
        ScanProgress = job.Progress;
        MetadataFilePath = job.MetadataFilePath;

        ScanStatus = job.Status switch
        {
            MetadataScanJobStatus.Queued => "Metadata scan queued.",
            MetadataScanJobStatus.Processing => "Scanning metadata...",
            MetadataScanJobStatus.Completed => $"Metadata saved to {job.MetadataFilePath}",
            MetadataScanJobStatus.Failed => $"Metadata scan failed: {job.ErrorMessage}",
            MetadataScanJobStatus.Cancelled => "Metadata scan cancelled.",
            _ => "Metadata scan status unknown."
        };
    }

    private void DetachCurrentScanJob()
    {
        if (currentScanJob is null)
        {
            return;
        }

        currentScanJob.StatusChanged -= OnScanJobStatusChanged;
        currentScanJob.ProgressChanged -= OnScanJobProgressChanged;
        currentScanJob = null;
    }
}
