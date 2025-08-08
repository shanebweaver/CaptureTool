namespace CaptureTool.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_ImageEdit_UndoRedo = new("ImageEdit_UndoRedo");
    public static readonly FeatureFlag Feature_VideoCapture = new("VideoCapture");
    public static readonly FeatureFlag Feature_ImageCapture_WindowMode = new("ImageCapture_WindowMode");
    public static readonly FeatureFlag Feature_ImageCapture_FreeformMode = new("ImageCapture_FreeformMode");
}
  