namespace CaptureTool.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_Capture = new("Capture");
    public static readonly FeatureFlag Feature_Capture_Image = new("Capture_Image");
    public static readonly FeatureFlag Feature_Capture_Image_Options = new("Capture_Image_Options");
    public static readonly FeatureFlag Feature_Capture_Video = new("Capture_Video");
    public static readonly FeatureFlag Feature_Capture_Video_Options = new("Capture_Video_Options");
    public static readonly FeatureFlag Feature_Capture_Audio = new("Capture_Audio");
    public static readonly FeatureFlag Feature_Capture_Audio_Options = new("Capture_Audio_Options");
}
  