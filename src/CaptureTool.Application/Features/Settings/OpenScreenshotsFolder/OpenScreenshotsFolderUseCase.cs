using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Features.Settings.OpenScreenshotsFolder;

public sealed class OpenScreenshotsFolderUseCase : IUseCase<OpenScreenshotsFolderRequest, OpenScreenshotsFolderResponse>, IConditional<OpenScreenshotsFolderRequest>
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public OpenScreenshotsFolderUseCase(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public Task<bool> CanExecuteAsync(OpenScreenshotsFolderRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

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