using CaptureTool.Services.Settings.Definitions;

namespace CaptureTool.Core;

public static partial class CaptureToolSettings
{
    public static readonly BoolSettingDefinition ImageCapture_Options_AutoSave = new("ImageCapture_Options_AutoSave", true);
}
