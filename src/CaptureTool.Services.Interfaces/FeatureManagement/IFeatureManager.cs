namespace CaptureTool.Services.Interfaces.FeatureManagement;

public interface IFeatureManager
{
    bool IsEnabled(FeatureFlag featureFlag);
}