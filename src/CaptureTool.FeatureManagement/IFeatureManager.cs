namespace CaptureTool.FeatureManagement;

public interface IFeatureManager
{
    bool IsEnabled(FeatureFlag featureFlag);
}
