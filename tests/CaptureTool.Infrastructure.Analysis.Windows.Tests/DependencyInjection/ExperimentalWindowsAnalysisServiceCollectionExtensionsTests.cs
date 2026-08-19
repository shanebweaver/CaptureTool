using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.Analyzers;
using CaptureTool.Infrastructure.Analysis.Windows.Experimental.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Analysis.Windows.Tests.DependencyInjection;

[TestClass]
public sealed class ExperimentalWindowsAnalysisServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddExperimentalWindowsAiAnalysis_ShouldRegisterSpeechAnalyzerExplicitly()
    {
        var services = new ServiceCollection();

        services.AddExperimentalWindowsAiAnalysis();

        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(IWindowsAiSpeechRecognitionService) &&
            descriptor.ImplementationType == typeof(WindowsAiSpeechRecognitionService) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(ICaptureAnalyzer) &&
            descriptor.ImplementationType == typeof(WindowsAiSpeechTranscriptAnalyzer) &&
            descriptor.Lifetime == ServiceLifetime.Singleton));
    }
}
