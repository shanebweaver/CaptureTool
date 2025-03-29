using System.Drawing;

namespace CaptureTool.Services.Settings.Definitions;

public class SizeSettingDefinition(string key, Size value) : SettingDefinition<Size>(key, value)
{
}
