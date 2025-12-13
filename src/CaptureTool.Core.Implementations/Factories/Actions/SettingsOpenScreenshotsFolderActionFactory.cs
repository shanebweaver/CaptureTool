using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces;

namespace CaptureTool.Core.Implementations.Factories.Actions;

public sealed partial class SettingsOpenScreenshotsFolderActionFactory : IFactoryServiceWithArgs<ISettingsOpenScreenshotsFolderAction, string>
{
    public ISettingsOpenScreenshotsFolderAction Create(string screenshotsFolderPath)
    {
        return new SettingsOpenScreenshotsFolderAction(screenshotsFolderPath);
    }
}
