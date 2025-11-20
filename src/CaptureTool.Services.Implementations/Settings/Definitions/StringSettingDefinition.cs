using CaptureTool.Services.Interfaces.Settings.Definitions;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

public sealed partial class StringSettingDefinition(string key, string value) 
    : SettingDefinition<string>(key, value), IStringSettingDefinition
{
}
