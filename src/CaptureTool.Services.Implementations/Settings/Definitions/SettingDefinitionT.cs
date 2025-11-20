using CaptureTool.Services.Interfaces.Settings.Definitions;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

public abstract partial class SettingDefinition<T>(string key, T value) 
    : SettingDefinition(key), ISettingDefinitionWithValue<T>
{
    public T Value { get; } = value;
}
