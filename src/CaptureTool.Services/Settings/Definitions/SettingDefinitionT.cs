namespace CaptureTool.Services.Settings.Definitions;

public abstract class SettingDefinition<T>(string key, T value) : SettingDefinition(key)
{
    public T Value { get; } = value;
}
