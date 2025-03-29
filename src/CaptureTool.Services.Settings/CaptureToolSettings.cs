using CaptureTool.Services.Settings.Definitions;

namespace CaptureTool.Services.Settings;

public static partial class CaptureToolSettings
{
    public static readonly BoolSettingDefinition ButtonClickedSetting = new("ButtonClicked", false);
}
