namespace CaptureTool.Infrastructure.Abstractions.Settings;

public interface ISettingDefinitionWithValue<T> : ISettingDefinition
{
    T Value { get; }
}