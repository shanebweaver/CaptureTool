using System.Drawing;

namespace CaptureTool.Services.Settings.Definitions;

public sealed partial class SizeSettingDefinition(string key, Size value) : SettingDefinition<Size>(key, value)
{
}