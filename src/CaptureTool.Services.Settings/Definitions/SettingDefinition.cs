using System.Text.Json.Serialization;

namespace CaptureTool.Services.Settings.Definitions;

[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract class SettingDefinition(string key)
{
    public string Key { get; } = key;
}
