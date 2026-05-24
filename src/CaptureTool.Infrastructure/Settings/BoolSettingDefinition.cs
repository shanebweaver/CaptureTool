using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Infrastructure.Settings;

public sealed partial class BoolSettingDefinition(string key, bool value)
    : SettingDefinition<bool>(key, value), IBoolSettingDefinition
{
}
