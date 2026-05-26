using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Settings;

public sealed partial class SettingsOpenScreenshotsFolderAppCommand : ISettingsOpenScreenshotsFolderAppCommand
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public SettingsOpenScreenshotsFolderAppCommand(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        var path = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = _storageService.GetSystemDefaultScreenshotsFolderPath();
        }

        if (Directory.Exists(path))
        {
            Process.Start("explorer.exe", $"/open, {path}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The screenshots folder path '{path}' does not exist.");
        }
    }
}
