using CaptureTool.Application.Abstractions.Settings;
using System.Text.Json.Serialization;

namespace CaptureTool.Application.Features.Settings.Definitions;

[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract partial class SettingDefinition(string key) : ISettingDefinition
{
    public string Key { get; } = key;
}
