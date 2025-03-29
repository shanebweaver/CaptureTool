using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;

namespace CaptureTool.FeatureManagement;

public sealed partial class CaptureToolFeatureManager : IFeatureManager
{
    private readonly FeatureManager _featureManager;

    public CaptureToolFeatureManager()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        ConfigurationFeatureDefinitionProvider configurationProvider = new(configuration);
        FeatureManagementOptions options = new()
        {
            IgnoreMissingFeatures = false,
        };
        FeatureManager featureManager = new(configurationProvider, options);

        _featureManager = featureManager;
    }

    public Task<bool> IsEnabledAsync(FeatureFlag featureFlag)
    {
        return _featureManager.IsEnabledAsync(featureFlag.Name);
    }
}
