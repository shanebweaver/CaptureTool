using CaptureTool.Infrastructure.Implementations.Settings;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.Settings;

public static partial class CaptureToolSettings
{
    public static readonly IBoolSettingDefinition Settings_ImageCapture_AutoCopy = new BoolSettingDefinition("Settings_ImageCapture_AutoCopy", true);
    public static readonly IBoolSettingDefinition Settings_VideoCapture_AutoCopy = new BoolSettingDefinition("Settings_VideoCapture_AutoCopy", true);
    public static readonly IBoolSettingDefinition Settings_ImageCapture_AutoSave = new BoolSettingDefinition("Settings_ImageCapture_AutoSave", true);
    public static readonly IBoolSettingDefinition Settings_VideoCapture_AutoSave = new BoolSettingDefinition("Settings_VideoCapture_AutoSave", true);
    public static readonly IStringSettingDefinition Settings_VideoCapture_AutoSaveFolder = new StringSettingDefinition("Settings_VideoCapture_VideosFolder", string.Empty);
    public static readonly IStringSettingDefinition Settings_ImageCapture_AutoSaveFolder = new StringSettingDefinition("Settings_ImageCapture_ScreenshotsFolder", string.Empty);
    public static readonly IStringSettingDefinition Settings_LanguageOverride = new StringSettingDefinition("Settings_LanguageOverride", string.Empty);
    public static readonly IBoolSettingDefinition VerboseLogging = new BoolSettingDefinition("VerboseLogging", false);
    public static readonly IBoolSettingDefinition Settings_VideoCapture_MetadataAutoSave = new BoolSettingDefinition("Settings_VideoCapture_MetadataAutoSave", true);
    public static readonly IBoolSettingDefinition Settings_VideoCapture_DefaultLocalAudioEnabled = new BoolSettingDefinition("Settings_VideoCapture_DefaultLocalAudioEnabled", true);
}
