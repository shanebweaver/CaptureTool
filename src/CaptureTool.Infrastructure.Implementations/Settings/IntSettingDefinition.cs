using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Infrastructure.Implementations.Settings;

public sealed partial class IntSettingDefinition(string key, int value)
    : SettingDefinition<int>(key, value), IIntSettingDefinition
{
}
