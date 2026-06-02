using CaptureTool.Application.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.Definitions;

public sealed partial class StringSettingDefinition(string key, string value)
    : SettingDefinition<string>(key, value), IStringSettingDefinition
{
}
