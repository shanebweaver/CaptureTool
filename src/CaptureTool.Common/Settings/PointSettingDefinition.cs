using System.Drawing;

namespace CaptureTool.Common.Settings;

public sealed partial class PointSettingDefinition(string key, Point value) 
    : SettingDefinition<Point>(key, value), IPointSettingDefinition
{
}
