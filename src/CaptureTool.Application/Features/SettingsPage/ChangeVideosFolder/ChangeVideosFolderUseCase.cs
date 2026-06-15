using CaptureTool.Application.Abstractions.Features.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.ChangeVideosFolder;

public sealed class ChangeVideosFolderUseCase : IChangeVideosFolderUseCase
{
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeVideosFolderUseCase(IFilePickerService picker, ISettingsService settings)
    {
        _picker = picker;
        _settings = settings;
    }

    public bool CanExecute(ChangeVideosFolderRequest request) => true;

    public async Task<ChangeVideosFolderResponse> ExecuteAsync(ChangeVideosFolderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var folder = await _picker.PickFolderAsync(UserFolder.Videos);
            if (folder is null)
            {
                return new ChangeVideosFolderResponse(false);
            }

            _settings.Set(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder, folder.FolderPath);
            await _settings.TrySaveAsync(cancellationToken);
            return new ChangeVideosFolderResponse();
        }
        catch (Exception)
        {
            return new ChangeVideosFolderResponse(false);
        }
    }
}
