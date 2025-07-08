namespace CaptureTool.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_ImageEdit_Print = new("ImageEdit_Print");
    public static readonly FeatureFlag Feature_ImageEdit_UndoRedo = new("ImageEdit_UndoRedo");
    public static readonly FeatureFlag Feature_ImageEdit_ChromaKey = new("ImageEdit_ChromaKey");
    public static readonly FeatureFlag Feature_VideoCapture = new("VideoCapture");
    public static readonly FeatureFlag Feature_UserFeedback = new("UserFeedback");
}
  