using CaptureTool.Services.Interfaces.Settings.Definitions;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

public sealed partial class BoolSettingDefinition(string key, bool value) 
    : SettingDefinition<bool>(key, value), IBoolSettingDefinition
{
}
