using CaptureTool.Application.Abstractions.Features.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Windowing;

namespace CaptureTool.Application.Features.Settings.ChangeVideosFolder;

public sealed class ChangeVideosFolderUseCase : IChangeVideosFolderUseCase
{
    private readonly IWindowHandleProvider _windowing;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeVideosFolderUseCase(IWindowHandleProvider windowing, IFilePickerService picker, ISettingsService settings)
    {
        _windowing = windowing;
        _picker = picker;
        _settings = settings;
    }

    public bool CanExecute(ChangeVideosFolderRequest request) => true;

    public async Task<ChangeVideosFolderResponse> ExecuteAsync(ChangeVideosFolderRequest request, CancellationToken cancellationToken = default)
    {
        var hwnd = _windowing.GetMainWindowHandle();
        var folder = await _picker.PickFolderAsync(hwnd, UserFolder.Videos) ?? throw new OperationCanceledException("No folder was selected.");
        _settings.Set(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder, folder.FolderPath);
        await _settings.TrySaveAsync(cancellationToken);
        return new ChangeVideosFolderResponse();
    }
}
