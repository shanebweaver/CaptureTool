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

    [TestMethod]
    public async Task Resolve_WhenPreferredProviderNeedsPreparation_ShouldNotSilentlyUseFallback()
    {
        FakeAnalyzer preferred = CreateAnalyzer("preferred", qualityTier: 10);
        preferred.Availability = CaptureAnalyzerAvailability.PreparationRequired;
        FakeAnalyzer fallback = CreateAnalyzer("fallback", qualityTier: 5);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([preferred, fallback]),
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

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.WaitingForPreparation, resolution.Status);
        Assert.IsNull(resolution.Analyzer);
        Assert.AreEqual(1, preferred.AvailabilityProbeCount);
        Assert.AreEqual(0, fallback.AvailabilityProbeCount);
        Assert.AreEqual(
            CaptureAnalyzerEligibilityStatus.PreparationRequired,
            resolution.Candidates[0].Eligibility);
        Assert.HasCount(1, resolution.Candidates);
    }

    [TestMethod]
    public async Task Resolve_ForBackgroundExecution_ShouldUseReadyFallbackWhilePreferredNeedsPreparation()
    {
        FakeAnalyzer preferred = CreateAnalyzer("preferred", qualityTier: 10);
        preferred.Availability = CaptureAnalyzerAvailability.PreparationRequired;
        FakeAnalyzer fallback = CreateAnalyzer("fallback", qualityTier: 5);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([preferred, fallback]),
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
                resolutionPolicyRevision: 1,
                allowReadyFallbackWhenPreparationRequired: true));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.Resolved, resolution.Status);
        Assert.AreSame(fallback, resolution.Analyzer);
        Assert.AreEqual(1, preferred.AvailabilityProbeCount);
        Assert.AreEqual(1, fallback.AvailabilityProbeCount);
        Assert.AreEqual(
            CaptureAnalyzerEligibilityStatus.PreparationRequired,
            resolution.Candidates[0].Eligibility);
        Assert.AreEqual(CaptureAnalyzerEligibilityStatus.Eligible, resolution.Candidates[1].Eligibility);
    }

    [TestMethod]
    public async Task Resolve_WhenPreferredProviderIsUnsupported_ShouldUseAvailableFallback()
    {
        FakeAnalyzer preferred = CreateAnalyzer("preferred", qualityTier: 10);
        preferred.Availability = CaptureAnalyzerAvailability.Unsupported(new AnalysisFailure(
            AnalysisFailureCode.CapabilityUnavailable,
            AnalysisFailureDisposition.Terminal));
        FakeAnalyzer fallback = CreateAnalyzer("fallback", qualityTier: 5);
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([preferred, fallback]),
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
        Assert.AreEqual(1, preferred.AvailabilityProbeCount);
        Assert.AreEqual(1, fallback.AvailabilityProbeCount);
    }

    [TestMethod]
    public async Task Resolve_WhenDeveloperPrefersAnalyzer_ShouldTryItBeforeHigherQualityCandidate()
    {
        FakeAnalyzer preferred = CreateAnalyzer("preferred", qualityTier: 1);
        FakeAnalyzer higherQuality = CreateAnalyzer("higher-quality", qualityTier: 100);
        var selection = new TestAnalyzerSelectionService(
            new CaptureAnalyzerSelection(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureAnalyzerSelectionMode.Prefer,
                new CaptureAnalyzerSelectionTarget("windows", "preferred")));
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([higherQuality, preferred]),
            new TestFeatureAvailability(),
            CreatePreference(),
            selection);
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
    public async Task Resolve_WhenDeveloperForcesAnalyzer_ShouldNotUseFallback()
    {
        FakeAnalyzer forced = CreateAnalyzer("forced", qualityTier: 1);
        forced.Availability = CaptureAnalyzerAvailability.Unsupported(new AnalysisFailure(
            AnalysisFailureCode.CapabilityUnavailable,
            AnalysisFailureDisposition.Terminal));
        FakeAnalyzer fallback = CreateAnalyzer("fallback", qualityTier: 100);
        var selection = new TestAnalyzerSelectionService(
            new CaptureAnalyzerSelection(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureAnalyzerSelectionMode.Force,
                new CaptureAnalyzerSelectionTarget("windows", "forced")));
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([fallback, forced]),
            new TestFeatureAvailability(),
            CreatePreference(),
            selection);
        AnalysisPurpose purpose = new("capture-memory-search", 1);

        CaptureAnalyzerResolution resolution = await resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureMediaKind.Image,
                sourceLength: 500,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose),
                resolutionPolicyRevision: 1));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.NoEligibleAnalyzer, resolution.Status);
        Assert.AreEqual(1, forced.AvailabilityProbeCount);
        Assert.AreEqual(0, fallback.AvailabilityProbeCount);
        Assert.AreEqual(
            CaptureAnalyzerEligibilityStatus.AnalyzerFeatureDisabled,
            resolution.Candidates.Single(candidate =>
                candidate.Descriptor.Identity.AnalyzerId == "fallback").Eligibility);
    }

    [TestMethod]
    public async Task Resolve_WhenDeveloperTurnsCapabilityOff_ShouldNotProbeCandidates()
    {
        FakeAnalyzer analyzer = CreateAnalyzer("disabled", qualityTier: 1);
        var selection = new TestAnalyzerSelectionService(
            new CaptureAnalyzerSelection(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureAnalyzerSelectionMode.Off));
        var resolver = new CaptureAnalyzerResolver(
            new CaptureAnalyzerCatalog([analyzer]),
            new TestFeatureAvailability(),
            CreatePreference(),
            selection);
        AnalysisPurpose purpose = new("capture-memory-search", 1);

        CaptureAnalyzerResolution resolution = await resolver.ResolveAsync(
            new CaptureAnalyzerResolutionRequest(
                AnalysisCapabilities.MediaPropertiesV1,
                CaptureMediaKind.Image,
                sourceLength: 500,
                purpose,
                AnalysisProcessingPolicy.LocalOnly(purpose),
                resolutionPolicyRevision: 1));

        Assert.AreEqual(CaptureAnalyzerResolutionStatus.NoEligibleAnalyzer, resolution.Status);
        Assert.AreEqual(0, analyzer.AvailabilityProbeCount);
        Assert.AreEqual(
            CaptureAnalyzerEligibilityStatus.AnalyzerFeatureDisabled,
            resolution.Candidates.Single().Eligibility);
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

        public CaptureAnalyzerAvailability Availability { get; set; } =
            CaptureAnalyzerAvailability.Available;

        public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
            CaptureAnalyzerAvailabilityRequest request,
            CancellationToken cancellationToken = default)
        {
            AvailabilityProbeCount++;
            if (ThrowOnAvailability)
            {
                throw new InvalidOperationException("Provider probe failed.");
            }

            return ValueTask.FromResult(Availability);
        }

        public Task<CaptureAnalyzerOutput> AnalyzeAsync(
            CaptureAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestAnalyzerSelectionService(CaptureAnalyzerSelection selection) :
        ICaptureAnalyzerSelectionService
    {
        public long Revision => 1;

        public CaptureAnalyzerSelection GetSelection(CapabilityDefinition capability) =>
            capability == selection.Capability
                ? selection
                : CaptureAnalyzerSelection.Automatic(capability);

        public int GetPreference(CaptureAnalyzerDescriptor descriptor) =>
            (selection.Mode is CaptureAnalyzerSelectionMode.Prefer or
                CaptureAnalyzerSelectionMode.Force) &&
            Matches(descriptor.Identity)
                ? 10_000
                : 0;

        public bool IsAllowed(CaptureAnalyzerDescriptor descriptor) => selection.Mode switch
        {
            CaptureAnalyzerSelectionMode.Off when descriptor.Capability == selection.Capability => false,
            CaptureAnalyzerSelectionMode.Force when descriptor.Capability == selection.Capability =>
                Matches(descriptor.Identity),
            _ => true,
        };

        public bool? GetFeatureEnabledOverride(AnalyzerIdentity analyzer) => null;

        public ValueTask<CaptureAnalyzerSelectionSaveResult> SaveAsync(
            IEnumerable<CaptureAnalyzerSelection> selections,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CaptureAnalyzerSelectionSaveResult(
                CaptureAnalyzerSelectionSaveStatus.Unavailable));

        private bool Matches(AnalyzerIdentity identity) =>
            string.Equals(selection.Target?.ProviderId, identity.ProviderId, StringComparison.Ordinal) &&
            string.Equals(selection.Target?.AnalyzerId, identity.AnalyzerId, StringComparison.Ordinal);
    }

    private sealed class TestFeatureAvailability : ICaptureAnalysisFeatureAvailability
    {
        public bool IsCaptureAnalysisEnabled { get; set; } = true;

        public long ResolutionPolicyRevision => 1;

        public bool IsProviderEnabled(string providerId) => IsCaptureAnalysisEnabled;

        public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer) => IsCaptureAnalysisEnabled;
    }
}
