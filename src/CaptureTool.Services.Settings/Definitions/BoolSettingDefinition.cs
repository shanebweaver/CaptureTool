namespace CaptureTool.Services.Settings.Definitions;

public class BoolSettingDefinition(string key, bool value) : SettingDefinition<bool>(key, value)
{
}
