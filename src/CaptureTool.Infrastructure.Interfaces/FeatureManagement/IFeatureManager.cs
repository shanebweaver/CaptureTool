namespace CaptureTool.Infrastructure.Interfaces.FeatureManagement;

public interface IFeatureManager
{
    bool IsEnabled(FeatureFlag featureFlag);
}