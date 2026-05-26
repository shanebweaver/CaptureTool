using CaptureTool.Application.Abstractions.VideoEdit;
using CaptureTool.Application.Settings;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.VideoEdit;

public class VideoEditSaveAppCommand : IVideoEditSaveAppCommand
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;
    private readonly ISettingsService _settingsService;
    private readonly IFeatureManager _featureManager;

    public VideoEditSaveAppCommand(
        IFilePickerService filePickerService,
        IWindowHandleProvider windowingService,
        ISettingsService settingsService,
        IFeatureManager featureManager)
    {
        _filePickerService = filePickerService;
        _windowingService = windowingService;
        _settingsService = settingsService;
        _featureManager = featureManager;
    }

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(string parameter)
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync(string videoPath, CancellationToken cancellationToken)
    {
        IsExecuting = true;

        try
        {
            if (string.IsNullOrEmpty(videoPath))
            {
                throw new InvalidOperationException("Cannot save video without a valid filepath.");
            }

            nint hwnd = _windowingService.GetMainWindowHandle();
            IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Video, UserFolder.Videos)
                ?? throw new OperationCanceledException("No file was selected.");

            cancellationToken.ThrowIfCancellationRequested();

            File.Copy(videoPath, file.FilePath, true);

            // Copy metadata file if it exists and the setting is enabled
            string metadataFilePath = Path.ChangeExtension(videoPath, MetadataFile.FileExtension);
            if (File.Exists(metadataFilePath) &&
                _featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection))
            {
                bool autoSaveMetadata = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave);
                if (autoSaveMetadata)
                {
                    string newMetadataFilePath = Path.ChangeExtension(file.FilePath, MetadataFile.FileExtension);
                    File.Copy(metadataFilePath, newMetadataFilePath, true);
                }
            }
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
