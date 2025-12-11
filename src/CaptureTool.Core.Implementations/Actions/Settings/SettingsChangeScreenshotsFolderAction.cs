using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Windowing;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsChangeScreenshotsFolderAction : AsyncActionCommand, ISettingsChangeScreenshotsFolderAction
{
    private readonly IWindowHandleProvider _windowing;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public SettingsChangeScreenshotsFolderAction(
        IWindowHandleProvider windowing,
        IFilePickerService picker,
        ISettingsService settings)
    {
        _windowing = windowing;
        _picker = picker;
        _settings = settings;
    }

    public override async Task ExecuteAsync()
    {
        var hwnd = _windowing.GetMainWindowHandle();
        var folder = await _picker.PickFolderAsync(hwnd, UserFolder.Pictures) ?? throw new OperationCanceledException("No folder was selected.");
        _settings.Set(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder, folder.FolderPath);
        await _settings.TrySaveAsync(CancellationToken.None);
    }
}
