using CaptureTool.Infrastructure.Interfaces.Settings;
using System.Drawing;

namespace CaptureTool.Infrastructure.Implementations.Settings;

public sealed partial class SizeSettingDefinition(string key, Size value)
    : SettingDefinition<Size>(key, value), ISizeSettingDefinition
{
}