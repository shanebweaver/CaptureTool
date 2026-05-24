using CaptureTool.Application.Settings;
using CaptureTool.Application.Abstractions.UseCases.Settings;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.UseCases.Settings;

public sealed partial class SettingsChangeVideosFolderUseCase : AsyncUseCase, ISettingsChangeVideosFolderUseCase
{
    private readonly IWindowHandleProvider _windowing;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public SettingsChangeVideosFolderUseCase(
        IWindowHandleProvider windowing,
        IFilePickerService picker,
        ISettingsService settings)
    {
        _windowing = windowing;
        _picker = picker;
        _settings = settings;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var hwnd = _windowing.GetMainWindowHandle();
        var folder = await _picker.PickFolderAsync(hwnd, UserFolder.Videos) ?? throw new OperationCanceledException("No folder was selected.");
        _settings.Set(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder, folder.FolderPath);
        await _settings.TrySaveAsync(cancellationToken);
    }
}
