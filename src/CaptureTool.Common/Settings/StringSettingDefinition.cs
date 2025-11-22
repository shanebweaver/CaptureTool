namespace CaptureTool.Common.Settings;

public sealed partial class StringSettingDefinition(string key, string value) 
    : SettingDefinition<string>(key, value), IStringSettingDefinition
{
}
