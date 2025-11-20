using CaptureTool.Services.Interfaces.Settings.Definitions;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

public sealed partial class IntSettingDefinition(string key, int value) 
    : SettingDefinition<int>(key, value), IIntSettingDefinition
{
}
