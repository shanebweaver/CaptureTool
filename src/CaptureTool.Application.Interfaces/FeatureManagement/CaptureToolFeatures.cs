using CaptureTool.Infrastructure.Interfaces.FeatureManagement;

namespace CaptureTool.Application.Interfaces.FeatureManagement;

public static partial class CaptureToolFeatures
{
    public static readonly FeatureFlag Feature_AddOns_Store = new("AddOns_Store");
    public static readonly FeatureFlag Feature_AudioCapture = new("AudioCapture");
    public static readonly FeatureFlag Feature_ImageEdit_ChromaKey = new("ImageEdit_ChromaKey");
    public static readonly FeatureFlag Feature_ImageEdit_Shapes = new("ImageEdit_Shapes");
    public static readonly FeatureFlag Feature_VideoCapture = new("VideoCapture");
    public static readonly FeatureFlag Feature_VideoCapture_LocalAudio = new("VideoCapture_LocalAudio");
    public static readonly FeatureFlag Feature_VideoCapture_MetadataCollection = new("VideoCapture_MetadataCollection");
    public static readonly FeatureFlag Feature_VideoCapture_MicrophoneSelection = new("VideoCapture_MicrophoneSelection");
}
