using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Infrastructure.Implementations.Settings;

public sealed partial class DoubleSettingDefinition(string key, double value)
    : SettingDefinition<double>(key, value), IDoubleSettingDefinition
{
}
