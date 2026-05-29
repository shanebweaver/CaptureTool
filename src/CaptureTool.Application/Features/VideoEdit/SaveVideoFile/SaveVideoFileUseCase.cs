using CaptureTool.Application.Abstractions;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Domain.Capture.Abstractions.Metadata;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Features.VideoEdit.SaveVideoFile;

public sealed class SaveVideoFileUseCase : IUseCase<SaveVideoFileRequest, SaveVideoFileResponse>, IConditional<SaveVideoFileRequest>
{
    private readonly IFilePickerService _filePickerService;
    private readonly IWindowHandleProvider _windowingService;
    private readonly ISettingsService _settingsService;
    private readonly IFeatureManager _featureManager;

    public SaveVideoFileUseCase(
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

    public Task<bool> CanExecuteAsync(SaveVideoFileRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(request.VideoPath));
    }

    public async Task<SaveVideoFileResponse> ExecuteAsync(SaveVideoFileRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.VideoPath))
        {
            throw new InvalidOperationException("Cannot save video without a valid filepath.");
        }

        nint hwnd = _windowingService.GetMainWindowHandle();
        IFile file = await _filePickerService.PickSaveFileAsync(hwnd, FilePickerType.Video, UserFolder.Videos)
            ?? throw new OperationCanceledException("No file was selected.");

        cancellationToken.ThrowIfCancellationRequested();

        File.Copy(request.VideoPath, file.FilePath, true);

        string metadataFilePath = Path.ChangeExtension(request.VideoPath, MetadataFile.FileExtension);
        if (File.Exists(metadataFilePath) &&
            _featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection) &&
            _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave))
        {
            string newMetadataFilePath = Path.ChangeExtension(file.FilePath, MetadataFile.FileExtension);
            File.Copy(metadataFilePath, newMetadataFilePath, true);
        }
        return new SaveVideoFileResponse();
    }
}