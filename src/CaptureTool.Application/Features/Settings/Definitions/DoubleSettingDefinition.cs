using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.Definitions;

public sealed partial class DoubleSettingDefinition(string key, double value)
    : SettingDefinition<double>(key, value), IDoubleSettingDefinition
{
}
