using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Analyzers;

[TestClass]
public sealed class CaptureAnalyzerResolutionPreferenceTests
{
    [TestMethod]
    public void GetPreference_ShouldMatchProviderAndAnalyzerWithoutFeatureKnowledge()
    {
        var preference = new CaptureAnalyzerResolutionPreference(
            [new CaptureAnalyzerPreferenceRule("provider", "adapter", 42)]);

        Assert.AreEqual(42, preference.GetPreference(CreateDescriptor("provider", "adapter")));
        Assert.AreEqual(0, preference.GetPreference(CreateDescriptor("provider", "other")));
        Assert.AreEqual(0, preference.GetPreference(CreateDescriptor("other", "adapter")));
    }

    [TestMethod]
    public void Constructor_ShouldRejectDuplicateRules()
    {
        var rules = new[]
        {
            new CaptureAnalyzerPreferenceRule("provider", "adapter", 1),
            new CaptureAnalyzerPreferenceRule("provider", "adapter", 2),
        };

        Assert.ThrowsExactly<ArgumentException>(() =>
            new CaptureAnalyzerResolutionPreference(rules));
    }

    [TestMethod]
    public void Contracts_ShouldRejectInvalidValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CaptureAnalyzerPreferenceRule(" ", "adapter", 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CaptureAnalyzerPreferenceRule("provider", " ", 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CaptureAnalyzerPreferenceRule("provider", "adapter", 10_001));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new CaptureAnalyzerResolutionPreference(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            new CaptureAnalyzerResolutionPreference([]).GetPreference(null!));
    }

    private static CaptureAnalyzerDescriptor CreateDescriptor(string providerId, string analyzerId)
    {
        var identity = new AnalyzerIdentity(
            analyzerId,
            providerId,
            null,
            null,
            "1",
            null,
            null,
            null,
            null);
        return new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.MediaPropertiesV1,
            identity,
            [CaptureMediaKind.Image],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.None,
            CaptureAnalyzerWorkloadClass.Lightweight,
            maximumSourceBytes: null,
            qualityTier: 1);
    }
}
