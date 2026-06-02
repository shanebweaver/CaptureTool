using System.Text.Json.Serialization;

namespace CaptureTool.Application.Abstractions.Settings.Definitions;

[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract partial class SettingDefinition(string key) : ISettingDefinition
{
    public string Key { get; } = key;
}
