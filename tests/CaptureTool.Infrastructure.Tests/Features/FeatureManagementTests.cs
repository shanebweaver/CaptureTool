using CaptureTool.FeatureManagement;
using CaptureTool.FeatureManagement.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CaptureTool.Infrastructure.Tests.Features;

[TestClass]
public sealed class FeatureManagementTests
{
    [TestMethod]
    public void MicrosoftFeatureManager_ShouldReturnConfiguredFlagState()
    {
        var manager = new MicrosoftFeatureManager();

        Assert.IsTrue(manager.IsEnabled(CreateFeatureFlag("enabled", true)));
        Assert.IsFalse(manager.IsEnabled(CreateFeatureFlag("disabled", false)));
    }

    [TestMethod]
    public void CaptureAnalysisReleaseFlags_ShouldBeDisabledByDefault()
    {
        var manager = new MicrosoftFeatureManager();

        Assert.IsFalse(manager.IsEnabled(AppFeatures.Feature_CaptureAnalysis_Platform));
        Assert.IsFalse(manager.IsEnabled(AppFeatures.Feature_CaptureMemory_Search));
    }

    [TestMethod]
    public void AddFeatureManagementServices_ShouldRegisterFeatureManager()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddFeatureManagementServices();

        Assert.AreSame(services, result);
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IFeatureManager) &&
            descriptor.ImplementationType == typeof(MicrosoftFeatureManager) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
    }

    private static FeatureFlag CreateFeatureFlag(string id, bool isEnabled)
    {
        ConstructorInfo constructor = typeof(FeatureFlag)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();

        return (FeatureFlag)constructor.Invoke([id, isEnabled]);
    }
}
