using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces.Storage;
using System.Diagnostics;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsOpenTempFolderAction : ActionCommand, ISettingsOpenTempFolderAction
{
    private readonly IStorageService _storageService;

    public SettingsOpenTempFolderAction(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public override void Execute()
    {
        var tempFolder = _storageService.GetApplicationTemporaryFolderPath();
        if (Directory.Exists(tempFolder))
        {
            Process.Start("explorer.exe", $"/open, {tempFolder}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The temporary folder path '{tempFolder}' does not exist.");
        }
    }
}
