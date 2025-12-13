using CaptureTool.Core.Implementations.Actions.Settings;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces;

namespace CaptureTool.Core.Implementations.Factories.Actions;

public sealed partial class SettingsOpenTempFolderActionFactory : IFactoryServiceWithArgs<ISettingsOpenTempFolderAction, string>
{
    public ISettingsOpenTempFolderAction Create(string tempFolderPath)
    {
        return new SettingsOpenTempFolderAction(tempFolderPath);
    }
}
