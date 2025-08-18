namespace CaptureTool.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_AddOns_Store = new("AddOns_Store");
    public static readonly FeatureFlag Feature_ImageEdit_ChromaKey = new("ImageEdit_ChromaKey");
    public static readonly FeatureFlag Feature_ImageEdit_UndoRedo = new("ImageEdit_UndoRedo");
    public static readonly FeatureFlag Feature_VideoCapture = new("VideoCapture");
    public static readonly FeatureFlag Feature_ImageCapture_FreeformMode = new("ImageCapture_FreeformMode");
}
  