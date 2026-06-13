using CaptureTool.Application.Abstractions.Features.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Features.SettingsPage.OpenTempFolder;

public sealed class OpenTempFolderUseCase : IOpenTempFolderUseCase
{
    private readonly IStorageService _storageService;

    public OpenTempFolderUseCase(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public bool CanExecute(OpenTempFolderRequest request) => true;

    public Task<OpenTempFolderResponse> ExecuteAsync(OpenTempFolderRequest request, CancellationToken cancellationToken = default)
    {
        var tempFolderPath = _storageService.GetApplicationTemporaryFolderPath();
        if (Directory.Exists(tempFolderPath))
        {
            Process.Start("explorer.exe", $"/open, {tempFolderPath}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The temporary folder path '{tempFolderPath}' does not exist.");
        }

        return Task.FromResult(new OpenTempFolderResponse());
    }
}