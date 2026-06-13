using CaptureTool.Application.Abstractions.Features.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;
using System.Diagnostics;

namespace CaptureTool.Application.Features.SettingsPage.OpenScreenshotsFolder;

public sealed class OpenScreenshotsFolderUseCase : IOpenScreenshotsFolderUseCase
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public OpenScreenshotsFolderUseCase(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public bool CanExecute(OpenScreenshotsFolderRequest request) => true;

    public Task<OpenScreenshotsFolderResponse> ExecuteAsync(OpenScreenshotsFolderRequest request, CancellationToken cancellationToken = default)
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

        return Task.FromResult(new OpenScreenshotsFolderResponse());
    }
}
