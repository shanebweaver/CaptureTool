using System.Text.Json.Serialization;

namespace CaptureTool.Services.Settings.Definitions;

[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract partial class SettingDefinition(string key)
{
    public string Key { get; } = key;
}
