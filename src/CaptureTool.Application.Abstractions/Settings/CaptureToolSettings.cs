using CaptureTool.Application.Abstractions.Settings.Definitions;

namespace CaptureTool.Application.Abstractions.Settings;

public static partial class CaptureToolSettings
{
    public static readonly IBoolSettingDefinition Settings_ImageCapture_AutoCopy = new BoolSettingDefinition("Settings_ImageCapture_AutoCopy", true);
    public static readonly IBoolSettingDefinition Settings_VideoCapture_AutoCopy = new BoolSettingDefinition("Settings_VideoCapture_AutoCopy", true);
    public static readonly IBoolSettingDefinition Settings_AudioCapture_AutoCopy = new BoolSettingDefinition("Settings_AudioCapture_AutoCopy", true);
    public static readonly IBoolSettingDefinition Settings_ImageCapture_AutoSave = new BoolSettingDefinition("Settings_ImageCapture_AutoSave", true);
    public static readonly IBoolSettingDefinition Settings_VideoCapture_AutoSave = new BoolSettingDefinition("Settings_VideoCapture_AutoSave", true);
    public static readonly IBoolSettingDefinition Settings_AudioCapture_AutoSave = new BoolSettingDefinition("Settings_AudioCapture_AutoSave", true);
    public static readonly IStringSettingDefinition Settings_VideoCapture_AutoSaveFolder = new StringSettingDefinition("Settings_VideoCapture_VideosFolder", string.Empty);
    public static readonly IStringSettingDefinition Settings_ImageCapture_AutoSaveFolder = new StringSettingDefinition("Settings_ImageCapture_ScreenshotsFolder", string.Empty);
    public static readonly IStringSettingDefinition Settings_AudioCapture_AutoSaveFolder = new StringSettingDefinition("Settings_AudioCapture_AudioFolder", string.Empty);
    public static readonly IStringSettingDefinition Settings_LanguageOverride = new StringSettingDefinition("Settings_LanguageOverride", string.Empty);
    public static readonly IBoolSettingDefinition VerboseLogging = new BoolSettingDefinition("VerboseLogging", false);
    public static readonly IBoolSettingDefinition Settings_Telemetry_IsEnabled = new BoolSettingDefinition("Settings_Telemetry_IsEnabled", false);
    public static readonly IStringSettingDefinition Settings_Telemetry_InstallId = new StringSettingDefinition("Settings_Telemetry_InstallId", string.Empty);
    public static readonly IBoolSettingDefinition Settings_VideoCapture_DefaultLocalAudioEnabled = new BoolSettingDefinition("Settings_VideoCapture_DefaultLocalAudioEnabled", true);
    public static readonly IBoolSettingDefinition Settings_AudioCapture_DefaultLocalAudioEnabled = new BoolSettingDefinition("Settings_AudioCapture_DefaultLocalAudioEnabled", true);
    public static readonly IBoolSettingDefinition Settings_Edit_WarnBeforeDiscard = new BoolSettingDefinition("Settings_Edit_WarnBeforeDiscard", true);
    public static readonly IBoolSettingDefinition Settings_Capture_WarnBeforeDiscard = new BoolSettingDefinition("Settings_Capture_WarnBeforeDiscard", true);
}
