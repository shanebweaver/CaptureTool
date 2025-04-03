namespace CaptureTool.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_DesktopCapture = new("DesktopCapture");
    public static readonly FeatureFlag Feature_AudioCapture = new("AudioCapture");
    public static readonly FeatureFlag Feature_VideoCapture = new("VideoCapture");
}
