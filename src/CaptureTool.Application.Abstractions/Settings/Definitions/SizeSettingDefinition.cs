using System.Drawing;

namespace CaptureTool.Application.Abstractions.Settings.Definitions;

public sealed partial class SizeSettingDefinition(string key, Size value)
    : SettingDefinition<Size>(key, value), ISizeSettingDefinition
{
}