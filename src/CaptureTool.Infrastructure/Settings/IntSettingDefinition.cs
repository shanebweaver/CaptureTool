using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Infrastructure.Settings;

public sealed partial class IntSettingDefinition(string key, int value)
    : SettingDefinition<int>(key, value), IIntSettingDefinition
{
}
