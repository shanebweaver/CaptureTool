using CaptureTool.Services.Interfaces.Settings.Definitions;
using System.Text.Json.Serialization;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract partial class SettingDefinition(string key) : ISettingDefinition
{
    public string Key { get; } = key;
}
