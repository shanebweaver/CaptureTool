using CaptureTool.Services.Settings.Definitions;

namespace CaptureTool.Core;

public static partial class CaptureToolSettings
{
    public static readonly BoolSettingDefinition DesktopImageCapture_Options_AutoSave = new("DesktopImageCapture_Options_AutoSave", true);
    public static readonly BoolSettingDefinition DesktopVideoCapture_Options_AutoSave = new("DesktopVideoCapture_Options_AutoSave", true);
}
