using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.Definitions;

public sealed partial class IntSettingDefinition(string key, int value)
    : SettingDefinition<int>(key, value), IIntSettingDefinition
{
}
