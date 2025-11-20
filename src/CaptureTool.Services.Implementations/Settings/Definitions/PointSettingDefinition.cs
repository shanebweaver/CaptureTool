using CaptureTool.Services.Interfaces.Settings.Definitions;
using System.Drawing;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

public sealed partial class PointSettingDefinition(string key, Point value) 
    : SettingDefinition<Point>(key, value), IPointSettingDefinition
{
}
