using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Storage;
using System.Diagnostics;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsOpenScreenshotsFolderAction : ActionCommand, ISettingsOpenScreenshotsFolderAction
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public SettingsOpenScreenshotsFolderAction(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public override void Execute()
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
