namespace CaptureTool.Common.Settings;

public sealed partial class IntSettingDefinition(string key, int value) 
    : SettingDefinition<int>(key, value), IIntSettingDefinition
{
}
