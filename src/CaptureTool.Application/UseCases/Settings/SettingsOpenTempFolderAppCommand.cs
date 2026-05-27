using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.UseCases.Settings;

internal class SettingsOpenTempFolderAppCommand : ISettingsOpenTempFolderAppCommand
{
    private readonly IStorageService _storageService;

    public SettingsOpenTempFolderAppCommand(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
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
    }
}
