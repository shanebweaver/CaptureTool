using CaptureTool.Infrastructure.Abstractions.Settings;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Settings;

[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract partial class SettingDefinition(string key) : ISettingDefinition
{
    public string Key { get; } = key;
}
