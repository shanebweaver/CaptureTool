namespace CaptureTool.Services.Settings.Definitions;

public class IntSettingDefinition(string key, int value) : SettingDefinition<int>(key, value)
{
}
