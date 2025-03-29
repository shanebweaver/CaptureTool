using System.Drawing;

namespace CaptureTool.Services.Settings.Definitions;

public class PointSettingDefinition(string key, Point value) : SettingDefinition<Point>(key, value)
{
}
