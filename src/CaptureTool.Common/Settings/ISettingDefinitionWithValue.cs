namespace CaptureTool.Common.Settings;

public interface ISettingDefinitionWithValue<T> : ISettingDefinition
{
    T Value { get; }
}