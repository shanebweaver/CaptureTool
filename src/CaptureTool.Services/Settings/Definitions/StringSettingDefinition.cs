namespace CaptureTool.Services.Settings.Definitions;

public sealed partial class StringSettingDefinition(string key, string value) : SettingDefinition<string>(key, value)
{
}
