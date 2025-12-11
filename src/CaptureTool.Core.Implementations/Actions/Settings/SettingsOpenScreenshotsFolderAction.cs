using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using System.Diagnostics;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsOpenScreenshotsFolderAction : ActionCommand<string>, ISettingsOpenScreenshotsFolderAction
{
    public override void Execute(string screenshotsFolderPath)
    {
        if (Directory.Exists(screenshotsFolderPath))
        {
            Process.Start("explorer.exe", $"/open, {screenshotsFolderPath}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The screenshots folder path '{screenshotsFolderPath}' does not exist.");
        }
    }
}
