namespace CaptureTool.Services.Settings.Definitions;

public class StringSettingDefinition(string key, string value) : SettingDefinition<string>(key, value)
{
}
