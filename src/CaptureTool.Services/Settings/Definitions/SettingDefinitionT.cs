namespace CaptureTool.Services.Settings.Definitions;

public abstract partial class SettingDefinition<T>(string key, T value) : SettingDefinition(key)
{
    public T Value { get; } = value;
}
