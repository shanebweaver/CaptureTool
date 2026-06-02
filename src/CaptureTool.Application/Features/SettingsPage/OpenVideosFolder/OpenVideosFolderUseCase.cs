using CaptureTool.Application.Abstractions.Features.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Features.Settings.OpenVideosFolder;

public sealed class OpenVideosFolderUseCase : IOpenVideosFolderUseCase
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public OpenVideosFolderUseCase(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public bool CanExecute(OpenVideosFolderRequest request) => true;

    public Task<OpenVideosFolderResponse> ExecuteAsync(OpenVideosFolderRequest request, CancellationToken cancellationToken = default)
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

        return Task.FromResult(new OpenVideosFolderResponse());
    }
}
