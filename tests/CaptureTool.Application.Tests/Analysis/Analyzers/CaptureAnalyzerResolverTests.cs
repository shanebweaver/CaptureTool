using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Analyzers;

[TestClass]
public sealed class CaptureAnalyzerResolverTests
{
    [TestMethod]
    public void Catalog_ShouldRejectDuplicateDescriptors()
    {
        FakeAnalyzer analyzer = CreateAnalyzer("same", qualityTier: 1);

        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalyzerCatalog(
            [analyzer, analyzer]));
    }

    [TestMethod]
    public async Task Resolve_ShouldFilterUnauthorizedBoundaryBeforeAvailabilityProbe()
    {
        FakeAnalyzer remote = CreateAnalyzer(
            "remote",
            qualityTier: 10,
            boundary: ProcessingBoundary.Remote);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([remote]),
            new TestFeatureAvailability(),
            CreatePreference());
        AnalysisPurpose purpose = new("capture-memory-search", 1);

        CaptureAnalyzerResolution resolution = await resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureMediaKind.Image,
                sourceLength: 1_024,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose),
                resolutionPolicyRevision: 1));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.NoEligibleAnalyzer, resolution.Status);
        Assert.AreEqual(CaptureAnalyzerEligibilityStatus.BoundaryNotAuthorized,
            resolution.Candidates.Single().Eligibility);
        Assert.AreEqual(0, remote.AvailabilityProbeCount);
    }

    [TestMethod]
    public async Task Resolve_ShouldPreferQualityThenOnDeviceThenLightweight()
    {
        FakeAnalyzer low = CreateAnalyzer("low", qualityTier: 1);
        FakeAnalyzer highHeavy = CreateAnalyzer(
            "high-heavy",
            qualityTier: 5,
            workload: CaptureAnalyzerWorkloadClass.AiIntensive);
        FakeAnalyzer highLight = CreateAnalyzer(
            "high-light",
            qualityTier: 5,
            workload: CaptureAnalyzerWorkloadClass.Lightweight);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([low, highHeavy, highLight]),
            new TestFeatureAvailability(),
            CreatePreference());
        AnalysisPurpose purpose = new("capture-memory-search", 1);

        CaptureAnalyzerResolution resolution = await resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureMediaKind.Image,
                sourceLength: 500,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose),
                resolutionPolicyRevision: 1));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.Resolved, resolution.Status);
        Assert.AreSame(highLight, resolution.Analyzer);
        Assert.AreEqual(1, highLight.AvailabilityProbeCount);
        Assert.AreEqual(0, highHeavy.AvailabilityProbeCount);
        Assert.AreEqual(0, low.AvailabilityProbeCount);
    }

    [TestMethod]
    public async Task Resolve_ShouldApplyInternalPreferenceBeforeDescriptorQuality()
    {
        FakeAnalyzer preferred = CreateAnalyzer("preferred", qualityTier: 1);
        FakeAnalyzer higherQuality = CreateAnalyzer("higher-quality", qualityTier: 100);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([higherQuality, preferred]),
            new TestFeatureAvailability(),
            CreatePreference(new CaptureAnalyzerPreferenceRule("windows", "preferred", 10)));
        AnalysisPurpose purpose = new("capture-memory-search", 1);

        CaptureAnalyzerResolution resolution = await resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureMediaKind.Image,
                sourceLength: 500,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose),
                resolutionPolicyRevision: 1));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.Resolved, resolution.Status);
        Assert.AreSame(preferred, resolution.Analyzer);
        Assert.AreEqual(1, preferred.AvailabilityProbeCount);
        Assert.AreEqual(0, higherQuality.AvailabilityProbeCount);
    }

    [TestMethod]
    public async Task Resolve_WhenFeatureDisabled_ShouldNotProbeAnyAnalyzer()
    {
        FakeAnalyzer analyzer = CreateAnalyzer("disabled", qualityTier: 1);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([analyzer]),
            new TestFeatureAvailability { IsCaptureAnalysisEnabled = false },
            CreatePreference());
        AnalysisPurpose purpose = new("capture-memory-search", 1);

        CaptureAnalyzerResolution resolution = await resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureMediaKind.Image,
                sourceLength: 1,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose),
                resolutionPolicyRevision: 1));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.FeatureDisabled, resolution.Status);
        Assert.AreEqual(0, analyzer.AvailabilityProbeCount);
    }

    [TestMethod]
    public async Task Resolve_WhenPreferredProviderThrows_ShouldContinueToNextCandidate()
    {
        FakeAnalyzer failing = CreateAnalyzer("failing", qualityTier: 10);
        failing.ThrowOnAvailability = true;
        FakeAnalyzer fallback = CreateAnalyzer("fallback", qualityTier: 5);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([failing, fallback]),
            new TestFeatureAvailability(),
            CreatePreference());
        AnalysisPurpose purpose = new("capture-memory-search", 1);

        CaptureAnalyzerResolution resolution = await resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureMediaKind.Image,
                sourceLength: 1,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose),
                resolutionPolicyRevision: 1));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.Resolved, resolution.Status);
        Assert.AreSame(fallback, resolution.Analyzer);
        Assert.AreEqual(1, failing.AvailabilityProbeCount);
        Assert.AreEqual(1, fallback.AvailabilityProbeCount);
    }

    private static FakeAnalyzer CreateAnalyzer(
        string id,
        int qualityTier,
        ProcessingBoundary boundary = ProcessingBoundary.OnDevice,
        CaptureAnalyzerWorkloadClass workload = CaptureAnalyzerWorkloadClass.Lightweight)
    {
        var identity = new AnalyzerIdentity(
            id,
            boundary == ProcessingBoundary.Remote ? "remote-provider" : "windows",
            null,
            null,
            "1",
            null,
            null,
            null,
            null);
        var descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.MediaPropertiesV1,
            identity,
            [CaptureMediaKind.Image],
            boundary,
            boundary == ProcessingBoundary.Remote
                ? CaptureAnalyzerDataKind.MediaProperties
                : CaptureAnalyzerDataKind.None,
            boundary == ProcessingBoundary.Remote
                ? CaptureAnalyzerRequirement.NetworkConnectivity
                : CaptureAnalyzerRequirement.None,
            workload,
            maximumSourceBytes: null,
            qualityTier);
        return new(descriptor);
    }

    private static CaptureAnalyzerResolutionPreference CreatePreference(
        params CaptureAnalyzerPreferenceRule[] rules)
    {
        return new(rules);
    }

    private sealed class FakeAnalyzer(CaptureAnalyzerDescriptor descriptor) : ICaptureAnalyzer
    {
        public CaptureAnalyzerDescriptor Descriptor { get; } = descriptor;

        public int AvailabilityProbeCount { get; private set; }

        public bool ThrowOnAvailability { get; set; }

        public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
            CaptureAnalyzerAvailabilityRequest request,
            CancellationToken cancellationToken = default)
        {
            AvailabilityProbeCount++;
            if (ThrowOnAvailability)
            {
                throw new InvalidOperationException("Provider probe failed.");
            }

            return ValueTask.FromResult(CaptureAnalyzerAvailability.Available);
        }

        public Task<CaptureAnalyzerOutput> AnalyzeAsync(
            CaptureAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestFeatureAvailability : ICaptureAnalysisFeatureAvailability
    {
        public bool IsCaptureAnalysisEnabled { get; set; } = true;

        public long ResolutionPolicyRevision => 1;

        public bool IsProviderEnabled(string providerId) => IsCaptureAnalysisEnabled;

        public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer) => IsCaptureAnalysisEnabled;
    }
}
