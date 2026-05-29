namespace CaptureTool.FeatureManagement;

public sealed partial class MicrosoftFeatureManager : IFeatureManager
{
    public bool IsEnabled(FeatureFlag featureFlag)
    {
        return featureFlag.IsEnabled;
    }
}
