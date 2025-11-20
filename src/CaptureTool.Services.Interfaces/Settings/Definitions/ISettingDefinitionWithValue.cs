namespace CaptureTool.Services.Interfaces.Settings.Definitions;

public interface ISettingDefinitionWithValue<T> : ISettingDefinition
{
    T Value { get; }
}