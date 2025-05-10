namespace CaptureTool.Services.Settings.Definitions;

public sealed partial class BoolSettingDefinition(string key, bool value) : SettingDefinition<bool>(key, value)
{
}
