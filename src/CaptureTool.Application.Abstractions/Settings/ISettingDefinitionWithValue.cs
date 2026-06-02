namespace CaptureTool.Application.Abstractions.Settings;

public interface ISettingDefinitionWithValue<T> : ISettingDefinition
{
    T Value { get; }
}