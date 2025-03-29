namespace CaptureTool.Services.Settings.Definitions;

public class StringArraySettingDefinition(string key, string[] value) : SettingDefinition<string[]>(key, value)
{
}
