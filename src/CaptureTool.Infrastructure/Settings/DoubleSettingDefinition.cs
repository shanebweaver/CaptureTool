using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Infrastructure.Settings;

public sealed partial class DoubleSettingDefinition(string key, double value)
    : SettingDefinition<double>(key, value), IDoubleSettingDefinition
{
}
