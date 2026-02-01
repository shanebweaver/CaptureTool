using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.UseCases.VideoEdit;
using CaptureTool.Domain.Capture.Interfaces.Metadata;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;

namespace CaptureTool.Application.Implementations.UseCases.VideoEdit;

public sealed partial class VideoEditSaveUseCase : AsyncUseCase<string>, IVideoEditSaveUseCase
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;
    private readonly ISettingsService _settingsService;
    private readonly IFeatureManager _featureManager;

    public VideoEditSaveUseCase(
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


    public override async Task ExecuteAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(videoPath))
        {
            throw new InvalidOperationException("Cannot save video without a valid filepath.");
        }

        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Video, UserFolder.Videos)
            ?? throw new OperationCanceledException("No file was selected.");

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
}
