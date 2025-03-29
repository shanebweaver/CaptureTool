namespace CaptureTool.Services.Settings.Definitions;

public class DoubleSettingDefinition(string key, double value) : SettingDefinition<double>(key, value)
{
}
