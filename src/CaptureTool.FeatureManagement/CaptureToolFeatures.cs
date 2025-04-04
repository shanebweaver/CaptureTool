namespace CaptureTool.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_DesktopCapture = new("DesktopCapture");
    public static readonly FeatureFlag Feature_AudioCapture = new("AudioCapture");
    public static readonly FeatureFlag Feature_CameraCapture = new("CameraCapture");
    public static readonly FeatureFlag Feature_DesktopCaptureOptions = new("DesktopCaptureOptions");
    public static readonly FeatureFlag Feature_AudioCaptureOptions = new("AudioCaptureOptions");
    public static readonly FeatureFlag Feature_CameraCaptureOptions = new("CameraCaptureOptions");
}
  