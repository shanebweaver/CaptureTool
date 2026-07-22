using System.Drawing;

namespace CaptureTool.Application.Abstractions.Settings.Definitions;

public sealed partial class PointSettingDefinition(string key, Point value)
    : SettingDefinition<Point>(key, value), IPointSettingDefinition
{
}
