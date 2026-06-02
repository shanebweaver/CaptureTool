using CaptureTool.Application.Abstractions.Settings;
using System.Drawing;

namespace CaptureTool.Application.Features.Settings.Definitions;

public sealed partial class SizeSettingDefinition(string key, Size value)
    : SettingDefinition<Size>(key, value), ISizeSettingDefinition
{
}