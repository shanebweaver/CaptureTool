using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Infrastructure.Implementations.Settings;

public sealed partial class StringSettingDefinition(string key, string value)
    : SettingDefinition<string>(key, value), IStringSettingDefinition
{
}
