namespace CaptureTool.Infrastructure.Interfaces.Settings;

public interface ISettingDefinitionWithValue<T> : ISettingDefinition
{
    T Value { get; }
}