namespace CaptureTool.Services.Settings.Definitions;

public sealed partial class IntSettingDefinition(string key, int value) : SettingDefinition<int>(key, value)
{
}
