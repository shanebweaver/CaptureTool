using CaptureTool.Common.Settings;

namespace CaptureTool.Core.Settings;

public static partial class CaptureToolSettings
{
    public static readonly IBoolSettingDefinition Settings_ImageCapture_AutoCopy = new BoolSettingDefinition("Settings_ImageCapture_AutoCopy", true);
    public static readonly IBoolSettingDefinition Settings_ImageCapture_AutoSave = new BoolSettingDefinition("Settings_ImageCapture_AutoSave", false);
    public static readonly IStringSettingDefinition Settings_ImageCapture_ScreenshotsFolder = new StringSettingDefinition("Settings_ImageCapture_ScreenshotsFolder", string.Empty);
    public static readonly IStringSettingDefinition Settings_LanguageOverride = new StringSettingDefinition("Settings_LanguageOverride", string.Empty);
}
