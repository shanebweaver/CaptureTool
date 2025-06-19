using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace CaptureTool.FeatureManagement;

public sealed partial class CaptureToolFeatureManager : IFeatureManager
{
    private readonly Dictionary<string, bool> _featureState = [];

    public CaptureToolFeatureManager()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var featureManagementSection = configuration.GetRequiredSection("feature_management");
        var featureFlagsSection = featureManagementSection.GetSection("feature_flags");
        var featureFlagSections = featureFlagsSection.GetChildren();

        foreach (var featureFlagSection in featureFlagSections)
        {
            string? id = featureFlagSection.GetSection("id").Value;
            string? enabled = featureFlagSection.GetSection("enabled").Value;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(enabled))
            {
                throw new System.InvalidOperationException("Failed to parse appsettings.json");
            }

            _featureState.Add(id, bool.Parse(enabled));
        }
    }

    public bool IsEnabled(FeatureFlag featureFlag)
    {
        return _featureState.TryGetValue(featureFlag.Id, out bool enabled) && enabled;
    }
}
