using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Windowing;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsChangeScreenshotsFolderUseCase : AsyncUseCase, ISettingsChangeScreenshotsFolderUseCase
{
    private readonly IWindowHandleProvider _windowing;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public SettingsChangeScreenshotsFolderUseCase(
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
        var folder = await _picker.PickFolderAsync(hwnd, UserFolder.Pictures) ?? throw new OperationCanceledException("No folder was selected.");
        _settings.Set(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder, folder.FolderPath);
        await _settings.TrySaveAsync(cancellationToken);
    }
}
