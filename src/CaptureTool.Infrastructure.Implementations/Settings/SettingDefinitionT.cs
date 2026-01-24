using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Infrastructure.Implementations.Settings;

public abstract partial class SettingDefinition<T>(string key, T value)
    : SettingDefinition(key), ISettingDefinitionWithValue<T>
{
    public T Value { get; } = value;
}
