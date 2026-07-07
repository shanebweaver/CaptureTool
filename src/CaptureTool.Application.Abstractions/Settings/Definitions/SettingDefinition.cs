using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Abstractions.Settings.Definitions;

public abstract partial class SettingDefinition(string key) : ISettingDefinition
{
    public string Key { get; } = key;
}
