using CaptureTool.Services.Interfaces.Settings.Definitions;
using System.Drawing;

namespace CaptureTool.Services.Implementations.Settings.Definitions;

public sealed partial class SizeSettingDefinition(string key, Size value) 
    : SettingDefinition<Size>(key, value), ISizeSettingDefinition
{
}