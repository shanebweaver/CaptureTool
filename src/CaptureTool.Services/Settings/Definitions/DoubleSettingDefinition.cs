namespace CaptureTool.Services.Settings.Definitions;

public sealed partial class DoubleSettingDefinition(string key, double value) : SettingDefinition<double>(key, value)
{
}
