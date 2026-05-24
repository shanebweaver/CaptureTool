using CaptureTool.Infrastructure.Abstractions.Settings;
using System.Drawing;

namespace CaptureTool.Infrastructure.Settings;

public sealed partial class PointSettingDefinition(string key, Point value)
    : SettingDefinition<Point>(key, value), IPointSettingDefinition
{
}
