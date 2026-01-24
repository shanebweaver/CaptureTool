using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Infrastructure.Implementations.Settings;

public sealed partial class BoolSettingDefinition(string key, bool value)
    : SettingDefinition<bool>(key, value), IBoolSettingDefinition
{
}
