using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Infrastructure.Settings;

public abstract partial class SettingDefinition<T>(string key, T value)
    : SettingDefinition(key), ISettingDefinitionWithValue<T>
{
    public T Value { get; } = value;
}
