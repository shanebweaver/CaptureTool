using CaptureTool.Application.Abstractions.Features.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;
using System.Diagnostics;

namespace CaptureTool.Application.Features.SettingsPage.OpenVideosFolder;

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
        try
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
                return Task.FromResult(new OpenVideosFolderResponse(false));
            }

            return Task.FromResult(new OpenVideosFolderResponse());
        }
        catch (Exception)
        {
            return Task.FromResult(new OpenVideosFolderResponse(false));
        }
    }
}
