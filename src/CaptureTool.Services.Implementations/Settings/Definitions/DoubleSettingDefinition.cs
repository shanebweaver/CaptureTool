using CaptureTool.Services.Interfaces.Settings.Definitions;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

public sealed partial class DoubleSettingDefinition(string key, double value) 
    : SettingDefinition<double>(key, value), IDoubleSettingDefinition
{
}
