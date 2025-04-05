namespace CaptureTool.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_DesktopCapture = new("DesktopCapture");
    public static readonly FeatureFlag Feature_DesktopCapture_Options = new("DesktopCapture_Options");
    public static readonly FeatureFlag Feature_DesktopCapture_Image = new("DesktopCapture_Image");
    public static readonly FeatureFlag Feature_DesktopCapture_Video = new("DesktopCapture_Video");

    public static readonly FeatureFlag Feature_AudioCapture = new("AudioCapture");
    public static readonly FeatureFlag Feature_AudioCapture_Options = new("AudioCapture_Options");

    public static readonly FeatureFlag Feature_CameraCapture = new("CameraCapture");
    public static readonly FeatureFlag Feature_CameraCapture_Options = new("CameraCapture_Options");
}
  