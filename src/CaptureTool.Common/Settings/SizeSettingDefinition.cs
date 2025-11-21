using System.Drawing;

namespace CaptureTool.Common.Settings;

public sealed partial class SizeSettingDefinition(string key, Size value) 
    : SettingDefinition<Size>(key, value), ISizeSettingDefinition
{
}