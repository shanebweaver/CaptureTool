using CaptureTool.Infrastructure.Interfaces.Settings;
using System.Drawing;

namespace CaptureTool.Infrastructure.Implementations.Settings;

public sealed partial class PointSettingDefinition(string key, Point value)
    : SettingDefinition<Point>(key, value), IPointSettingDefinition
{
}
