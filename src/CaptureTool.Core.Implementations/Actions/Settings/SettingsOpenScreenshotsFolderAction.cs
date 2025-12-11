using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using System.Diagnostics;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsOpenScreenshotsFolderAction : ActionCommand, ISettingsOpenScreenshotsFolderAction
{
    private readonly string _screenshotsFolderPath;

    public SettingsOpenScreenshotsFolderAction(string screenshotsFolderPath)
    {
        _screenshotsFolderPath = screenshotsFolderPath;
    }

    public override void Execute()
    {
        if (Directory.Exists(_screenshotsFolderPath))
        {
            Process.Start("explorer.exe", $"/open, {_screenshotsFolderPath}");
        }
        else
        {
            throw new DirectoryNotFoundException($"The screenshots folder path '{_screenshotsFolderPath}' does not exist.");
        }
    }
}
