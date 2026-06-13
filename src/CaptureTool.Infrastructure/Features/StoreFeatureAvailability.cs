using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.FeatureManagement;

namespace CaptureTool.Infrastructure.Features;

public sealed class StoreFeatureAvailability : IStoreFeatureAvailability
{
    private readonly IFeatureManager _featureManager;

    public StoreFeatureAvailability(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public bool IsStoreEnabled => _featureManager.IsEnabled(AppFeatures.Feature_AddOns_Store);
}
