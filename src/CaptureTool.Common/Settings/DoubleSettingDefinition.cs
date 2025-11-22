namespace CaptureTool.Common.Settings;

public sealed partial class DoubleSettingDefinition(string key, double value) 
    : SettingDefinition<double>(key, value), IDoubleSettingDefinition
{
}
