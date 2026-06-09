using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.Definitions;

public sealed partial class BoolSettingDefinition(string key, bool value)
    : SettingDefinition<bool>(key, value), IBoolSettingDefinition
{
}
