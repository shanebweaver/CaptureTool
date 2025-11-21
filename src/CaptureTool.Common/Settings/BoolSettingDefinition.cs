namespace CaptureTool.Common.Settings;

public sealed partial class BoolSettingDefinition(string key, bool value) 
    : SettingDefinition<bool>(key, value), IBoolSettingDefinition
{
}
