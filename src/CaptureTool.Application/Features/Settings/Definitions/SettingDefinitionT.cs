using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.Definitions;

public abstract partial class SettingDefinition<T>(string key, T value)
    : SettingDefinition(key), ISettingDefinitionWithValue<T>
{
    public T Value { get; } = value;
}
