using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using System.Diagnostics;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsOpenTempFolderUseCase : ActionCommand, ISettingsOpenTempFolderUseCase
{
    private readonly IStorageService _storageService;

    public SettingsOpenTempFolderUseCase(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public override void Execute()
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
