using CaptureTool.Infrastructure.Interfaces.Settings;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Implementations.Settings;

[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract partial class SettingDefinition(string key) : ISettingDefinition
{
    public string Key { get; } = key;
}
