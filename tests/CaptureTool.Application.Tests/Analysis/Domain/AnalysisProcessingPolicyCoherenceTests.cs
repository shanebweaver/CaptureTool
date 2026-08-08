using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Domain;

[TestClass]
public sealed class AnalysisProcessingPolicyCoherenceTests
{
    [TestMethod]
    public void Constructor_ShouldRequireRemoteProvidersExactlyWhenRemoteBoundaryIsAllowed()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisProcessingPolicy(
            AnalysisTestData.Purpose,
            [ProcessingBoundary.OnDevice, ProcessingBoundary.Remote]));
        Assert.ThrowsExactly<ArgumentException>(() => new AnalysisProcessingPolicy(
            AnalysisTestData.Purpose,
            [ProcessingBoundary.OnDevice],
            ["microsoft.azure"]));
    }

    [TestMethod]
    public void Constructor_ShouldNormalizeSortDeduplicateAndDefensivelyCopyPolicyCollections()
    {
        ProcessingBoundary[] boundaries =
        [
            ProcessingBoundary.Remote,
            ProcessingBoundary.OnDevice,
            ProcessingBoundary.Remote,
        ];
        string[] providerIds = [" Zebra.Provider ", "alpha.provider", "ALPHA.PROVIDER"];
        var policy = new AnalysisProcessingPolicy(
            AnalysisTestData.Purpose,
            boundaries,
            providerIds);

        boundaries[0] = ProcessingBoundary.Unknown;
        providerIds[0] = "changed.provider";

        CollectionAssert.AreEqual(
            new[] { ProcessingBoundary.OnDevice, ProcessingBoundary.Remote },
            policy.AllowedBoundaries.ToArray());
        CollectionAssert.AreEqual(
            new[] { "alpha.provider", "zebra.provider" },
            policy.AllowedRemoteProviderIds.ToArray());
    }

    [TestMethod]
    public void EquivalentPolicies_ShouldIgnoreInputOrderingAndDuplicateSpellings()
    {
        var first = new AnalysisProcessingPolicy(
            AnalysisTestData.Purpose,
            [ProcessingBoundary.Remote, ProcessingBoundary.OnDevice],
            ["zebra.provider", "alpha.provider"]);
        var second = new AnalysisProcessingPolicy(
            AnalysisTestData.Purpose,
            [ProcessingBoundary.OnDevice, ProcessingBoundary.Remote, ProcessingBoundary.OnDevice],
            [" ALPHA.PROVIDER ", "zebra.provider", "alpha.provider"]);

        Assert.IsTrue(first.IsEquivalentTo(second));
        Assert.AreEqual(first, second);
        Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
    }

    [TestMethod]
    public void EquivalentPolicies_ShouldRequireExactPurposeBoundaryAndProviderSet()
    {
        AnalysisPurpose changedPurpose = new(AnalysisTestData.Purpose.Id, 2);
        var local = AnalysisProcessingPolicy.LocalOnly(AnalysisTestData.Purpose);
        var changedPurposePolicy = AnalysisProcessingPolicy.LocalOnly(changedPurpose);
        var remote = new AnalysisProcessingPolicy(
            AnalysisTestData.Purpose,
            [ProcessingBoundary.OnDevice, ProcessingBoundary.Remote],
            ["microsoft.azure"]);

        Assert.IsFalse(local.IsEquivalentTo(changedPurposePolicy));
        Assert.IsFalse(local.IsEquivalentTo(remote));
        Assert.IsFalse(local.IsEquivalentTo(null));
    }
}
