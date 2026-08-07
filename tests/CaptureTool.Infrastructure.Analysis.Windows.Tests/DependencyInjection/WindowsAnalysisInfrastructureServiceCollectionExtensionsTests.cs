using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.DependencyInjection;

[TestClass]
public sealed class WindowsAnalysisInfrastructureServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddWindowsAnalysisDomains_ShouldRegisterBuiltInAnalyzersExplicitly()
    {
        var services = new ServiceCollection();

        services.AddWindowsAnalysisDomains();

        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.ImplementationType == typeof(WindowsImageMediaPropertiesAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
    }
}
