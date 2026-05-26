using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Windowing;

namespace CaptureTool.Application.Settings;

internal class SettingsChangeScreenshotsFolderAppCommand : ISettingsChangeScreenshotsFolderAppCommand
{
    private readonly IWindowHandleProvider _windowing;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public SettingsChangeScreenshotsFolderAppCommand(
        IWindowHandleProvider windowing,
        IFilePickerService picker,
        ISettingsService settings)
    {
        _windowing = windowing;
        _picker = picker;
        _settings = settings;
    }

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IsExecuting = true;

        try
        {
            var hwnd = _windowing.GetMainWindowHandle();
            var folder = await _picker.PickFolderAsync(hwnd, UserFolder.Pictures) ?? throw new OperationCanceledException("No folder was selected.");
            _settings.Set(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder, folder.FolderPath);
            await _settings.TrySaveAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
