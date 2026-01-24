using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsOpenVideosFolderUseCase : UseCase, ISettingsOpenVideosFolderUseCase
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public SettingsOpenVideosFolderUseCase(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public override void Execute()
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
