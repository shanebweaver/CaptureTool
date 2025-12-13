using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using System.Diagnostics;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsOpenTempFolderAction : ActionCommand, ISettingsOpenTempFolderAction
{
    private readonly string _tempFolderPath;

    public SettingsOpenTempFolderAction(string tempFolderPath)
    {
        _tempFolderPath = tempFolderPath;
    }

    public override void Execute()
    {
        if (Directory.Exists(_tempFolderPath))
        {
            Process.Start("explorer.exe", $"/open, {_tempFolderPath}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The temporary folder path '{_tempFolderPath}' does not exist.");
        }
    }
}
