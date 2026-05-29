using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Infrastructure.Settings;

public sealed partial class StringSettingDefinition(string key, string value)
    : SettingDefinition<string>(key, value), IStringSettingDefinition
{
}
