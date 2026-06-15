using CaptureTool.Application.Abstractions.Features.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.ChangeScreenshotsFolder;

public sealed class ChangeScreenshotsFolderUseCase : IChangeScreenshotsFolderUseCase
{
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeScreenshotsFolderUseCase(
        IFilePickerService picker,
        ISettingsService settings)
    {
        _picker = picker;
        _settings = settings;
    }

    public bool CanExecute(ChangeScreenshotsFolderRequest request) => true;

    public async Task<ChangeScreenshotsFolderResponse> ExecuteAsync(ChangeScreenshotsFolderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = await _picker.PickFolderAsync(UserFolder.Pictures);
            if (folder is null)
            {
                return new ChangeScreenshotsFolderResponse(false);
            }

            _settings.Set(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder, folder.FolderPath);
            await _settings.TrySaveAsync(cancellationToken);
            return new ChangeScreenshotsFolderResponse();
        }
        catch (Exception)
        {
            return new ChangeScreenshotsFolderResponse(false);
        }
    }
}
