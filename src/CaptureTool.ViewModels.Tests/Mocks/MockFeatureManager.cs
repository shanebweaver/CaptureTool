using CaptureTool.FeatureManagement;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockFeatureManager : IFeatureManager
{
    public bool IsEnabled(FeatureFlag featureFlag)
    {
        return true;
    }
}
