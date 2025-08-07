using CaptureTool.Services.Settings.Definitions;

namespace CaptureTool.Core;

public static partial class CaptureToolSettings
{
    public static readonly BoolSettingDefinition Settings_ImageCapture_AutoCopy = new("Settings_ImageCapture_AutoCopy", false);
    public static readonly BoolSettingDefinition Settings_ImageCapture_AutoSave = new("Settings_ImageCapture_AutoSave", false);
    public static readonly StringSettingDefinition Settings_ImageCapture_ScreenshotsFolder = new("Settings_ImageCapture_ScreenshotsFolder", string.Empty);
    public static readonly StringSettingDefinition Settings_LanguageOverride = new("Settings_LanguageOverride", string.Empty);
}
