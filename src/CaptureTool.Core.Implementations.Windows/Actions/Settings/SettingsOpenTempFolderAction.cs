using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces.Storage;
using System.Diagnostics;

namespace CaptureTool.Core.Implementations.Windows.Actions.Settings;

public sealed partial class SettingsOpenTempFolderAction : ActionCommand, ISettingsOpenTempFolderAction
{
    private readonly IStorageService _storageService;

    public SettingsOpenTempFolderAction(IStorageService storageService)
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
