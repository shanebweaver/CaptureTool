using CaptureTool.Services.Settings.Definitions;

namespace CaptureTool.Core;

public static partial class CaptureToolSettings
{
    public static readonly BoolSettingDefinition ButtonClickedSetting = new("ButtonClicked", false);

    public static readonly IntSettingDefinition ThemeSetting = new("Theme", 0);
}
