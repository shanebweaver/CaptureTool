namespace CaptureTool.Common.Settings;

public abstract partial class SettingDefinition<T>(string key, T value) 
    : SettingDefinition(key), ISettingDefinitionWithValue<T>
{
    public T Value { get; } = value;
}
