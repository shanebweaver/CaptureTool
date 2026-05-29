using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Features.Settings.OpenVideosFolder;

public sealed class OpenVideosFolderUseCase : IUseCase<OpenVideosFolderRequest, OpenVideosFolderResponse>, IConditional<OpenVideosFolderRequest>
{
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;

    public OpenVideosFolderUseCase(ISettingsService settingsService, IStorageService storageService)
    {
        _settingsService = settingsService;
        _storageService = storageService;
    }

    public Task<bool> CanExecuteAsync(OpenVideosFolderRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

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