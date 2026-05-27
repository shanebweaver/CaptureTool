using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.UseCases.Settings;

internal class SettingsOpenVideosFolderAppCommand : ISettingsOpenVideosFolderAppCommand
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public SettingsOpenVideosFolderAppCommand(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        var path = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = _storageService.GetSystemDefaultVideosFolderPath();
        }

        if (Directory.Exists(path))
        {
            Process.Start("explorer.exe", $"/open, {path}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The videos folder path '{path}' does not exist.");
        }
    }
}
