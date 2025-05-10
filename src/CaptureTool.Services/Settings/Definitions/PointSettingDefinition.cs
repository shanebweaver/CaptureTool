using System.Drawing;

namespace CaptureTool.Services.Settings.Definitions;

public sealed partial class PointSettingDefinition(string key, Point value) : SettingDefinition<Point>(key, value)
{
}
