using CaptureTool.Infrastructure.Abstractions.Settings;
using System.Drawing;

namespace CaptureTool.Infrastructure.Settings;

public sealed partial class SizeSettingDefinition(string key, Size value)
    : SettingDefinition<Size>(key, value), ISizeSettingDefinition
{
}