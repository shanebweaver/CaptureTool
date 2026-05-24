using CaptureTool.Application.Settings;
using CaptureTool.Application.Abstractions.UseCases.Settings;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.UseCases.Settings;

public sealed partial class SettingsOpenScreenshotsFolderUseCase : UseCase, ISettingsOpenScreenshotsFolderUseCase
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public SettingsOpenScreenshotsFolderUseCase(ISettingsService settingsService, IStorageService storageService)
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
