using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Analysis.Persistence;

namespace CaptureTool.Infrastructure.Tests.Analysis.Persistence;

[TestClass]
public sealed class CaptureAnalysisMutationCoordinatorTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 7, 21, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Register_WhenAuthorizationIsRevokedAfterVerification_ShouldRejectBeforeWrite()
    {
        TestContext context = CreateContext(
            authorizedCalls: 1,
            sourceGeneration: 7);

        CaptureAnalysisStoreWriteResult result = await context.Coordinator.TryRegisterSourceAsync(
            context.Registration,
            expectedDocumentRevision: null);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.StaleCommit, result.Status);
        Assert.AreEqual(1, context.SourceVerifier.OpenCount);
        Assert.IsNull(await context.MetadataStore.GetAsync(context.Registration.Preconditions.CaptureId));
        context.MetadataStore.Dispose();
    }

    [TestMethod]
    public async Task Register_WhenSourceGenerationChangesAfterAuthorization_ShouldRejectMetadataWrite()
    {
        TestContext context = CreateContext(
            authorizedCalls: int.MaxValue,
            sourceGeneration: 8);

        CaptureAnalysisStoreWriteResult result = await context.Coordinator.TryRegisterSourceAsync(
            context.Registration,
            expectedDocumentRevision: null);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.StaleCommit, result.Status);
        Assert.AreEqual(1, context.SourceVerifier.OpenCount);
        Assert.IsNull(await context.MetadataStore.GetAsync(context.Registration.Preconditions.CaptureId));
        context.MetadataStore.Dispose();
    }

    private static TestContext CreateContext(int authorizedCalls, long sourceGeneration)
    {
        CaptureId captureId = CaptureId.New();
        string path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"{captureId}.png"));
        var asset = new CaptureAsset(
            captureId,
            CaptureFileType.Image,
            path,
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc);
        CaptureAnalysisAuthorizationScope scope = CaptureAnalysisPolicyDefaults.CreateAuthorizationScope();
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(scope, 0);
        var recipe = new CaptureAnalysisRecipe(
            new AnalysisRecipeId("capture-memory-image"),
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Image,
            [new RecipeCapability(
                AnalysisCapabilities.MediaPropertiesV1,
                RecipeCapabilityRequirement.Required)]);
        var enrollment = new CaptureAnalysisEnrollment(
            captureId,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            assetFinalizationSequence: 1,
            recipe.Id,
            recipe.Version);
        var control = new CaptureAnalysisControlSnapshot(
            1,
            new CaptureAnalysisControlState(policy, [enrollment]));
        DateTimeOffset sourceTime = CapturedAtUtc.AddSeconds(1);
        var sourceRevision = new SourceRevision(
            20,
            sourceTime,
            ContentFingerprint.Sha256(new string('c', 64)));
        var preconditions = new AnalysisCommitPreconditions(
            captureId,
            captureSourceGeneration: 7,
            sourceRevision.ProvisionalStamp,
            sourceRevision,
            CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
            policy.PolicyRevision,
            policy.ControlGeneration,
            enrollment.EnrollmentGeneration,
            enrollment.TombstoneGeneration,
            recipe.Id,
            recipe.Version,
            resolutionPolicyRevision: 1);
        var registration = new CaptureAnalysisSourceRegistration(
            preconditions,
            CaptureMediaKind.Image,
            CapturedAtUtc,
            recipe);
        var source = new StubVerifiedSource(captureId, sourceGeneration, sourceRevision);
        var sourceVerifier = new RecordingSourceVerifier(source);
        var metadata = new LocalCaptureAnalysisStore(
            new TestLocalCachePathProvider(AnalysisPersistenceTestData.CreateTestFolder()),
            new TestDataProtectionService(),
            new AtomicFileWriter(),
            new TestLogService());
        var policyService = new StubPolicyService(control, scope, authorizedCalls);
        var coordinator = new CaptureAnalysisMutationCoordinator(
            new StubCaptureAssetCatalog(asset),
            new StubControlStore(control),
            policyService,
            new StubFeatureAvailability(),
            sourceVerifier,
            new CaptureAnalyzerCatalog([]),
            metadata);
        return new(coordinator, registration, sourceVerifier, metadata);
    }

    private sealed record TestContext(
        CaptureAnalysisMutationCoordinator Coordinator,
        CaptureAnalysisSourceRegistration Registration,
        RecordingSourceVerifier SourceVerifier,
        LocalCaptureAnalysisStore MetadataStore);

    private sealed class StubPolicyService(
        CaptureAnalysisControlSnapshot control,
        CaptureAnalysisAuthorizationScope scope,
        int authorizedCalls) : ICaptureAnalysisPolicyService
    {
        private int _authorizationCallCount;

        public ValueTask<CaptureAnalysisPolicySnapshot> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CaptureAnalysisPolicySnapshot(
                CaptureAnalysisPolicySnapshotStatus.Available,
                CaptureAnalysisConsentState.Granted,
                control));

        public ValueTask<CaptureAnalysisAdmissionDecision> AuthorizeAdmissionAsync(
            CaptureAnalysisAdmissionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<CaptureAnalysisAuthorizationDecision> AuthorizeAsync(
            CaptureAnalysisAuthorizationRequest request,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(
                ++_authorizationCallCount <= authorizedCalls
                ? CaptureAnalysisAuthorizationDecision.Authorized(
                    request,
                    control.State.PolicyRevision,
                    control.State.ControlGeneration,
                    enrollmentGeneration: 1,
                    tombstoneGeneration: 0,
                    scope)
                : CaptureAnalysisAuthorizationDecision.Denied(
                    request,
                    CaptureAnalysisPolicyDenialReason.CaptureForgotten,
                    control.State.PolicyRevision,
                    control.State.ControlGeneration,
                    enrollmentGeneration: 2,
                    tombstoneGeneration: 1,
                    scope));
    }

    private sealed class RecordingSourceVerifier(IVerifiedCaptureAnalysisSource source) :
        ICaptureAnalysisSourceVerifier
    {
        public int OpenCount { get; private set; }

        public ValueTask<IVerifiedCaptureAnalysisSource?> TryOpenVerifiedAsync(
            CaptureAnalysisSourceVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return ValueTask.FromResult<IVerifiedCaptureAnalysisSource?>(source);
        }
    }

    private sealed class StubVerifiedSource(
        CaptureId captureId,
        long sourceGeneration,
        SourceRevision sourceRevision) : IVerifiedCaptureAnalysisSource
    {
        public CaptureId CaptureId => captureId;
        public CaptureMediaKind MediaKind => CaptureMediaKind.Image;
        public long CaptureSourceGeneration => sourceGeneration;
        public ProvisionalSourceStamp SourceStamp => sourceRevision.ProvisionalStamp;
        public SourceRevision SourceRevision => sourceRevision;
        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(new byte[sourceRevision.Length]));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubCaptureAssetCatalog(CaptureAsset asset) : ICaptureAssetCatalog
    {
        public IReadOnlyList<CaptureAsset> GetAssets() => [asset];
        public CaptureAsset? Get(CaptureId captureId) => captureId == asset.Id ? asset : null;
        public CaptureAsset? FindByPath(string filePath) => asset;
        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) => [];
        public long GetLatestChangeSequence() => 1;
        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset added) => throw new NotSupportedException();
        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryUpdate(CaptureAsset updated, long expectedLifecycleRevision, CaptureAssetChangeType changeType) => throw new NotSupportedException();
    }

    private sealed class StubControlStore(CaptureAnalysisControlSnapshot snapshot) :
        ICaptureAnalysisControlStore
    {
        public ValueTask<CaptureAnalysisControlSnapshot> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(snapshot);
        public ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(CaptureAnalysisControlState state, long expectedDocumentRevision, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubFeatureAvailability : ICaptureAnalysisFeatureAvailability
    {
        public bool IsCaptureAnalysisEnabled => true;
        public long ResolutionPolicyRevision => 1;
        public bool IsProviderEnabled(string providerId) => true;
        public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer) => true;
    }
}
