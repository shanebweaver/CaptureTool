using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
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

    [TestMethod]
    public async Task Register_ShouldInvalidateResultFromUnregisteredProducerRevision()
    {
        AnalyzerIdentity currentProducer = CreateAnalyzerIdentity("2");
        var descriptor = new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.MediaPropertiesV1,
            currentProducer,
            [CaptureMediaKind.Image],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.None,
            CaptureAnalyzerWorkloadClass.Lightweight,
            maximumSourceBytes: null,
            qualityTier: 1);
        TestContext context = CreateContext(
            authorizedCalls: int.MaxValue,
            sourceGeneration: 7,
            [new StubAnalyzer(descriptor)]);
        AnalyzerIdentity oldProducer = CreateAnalyzerIdentity("1");
        var canonical = new CanonicalCapabilityResult(
            context.Registration.Preconditions.CaptureId,
            context.Registration.Preconditions.SourceRevision,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 100)),
            oldProducer,
            ProcessingBoundary.OnDevice,
            CapturedAtUtc.AddMinutes(1));
        var record = new CaptureAnalysisRecord(
            context.Registration.Preconditions.CaptureId,
            CaptureMediaKind.Image,
            CapturedAtUtc,
            context.Registration.Preconditions.SourceRevision,
            context.Registration.Recipe,
            [new CapabilityAnalysis(AnalysisCapabilities.MediaPropertiesV1, canonical, null)]);
        CaptureAnalysisStoreWriteResult initial = await context.MetadataStore.TryWriteAsync(
            record,
            expectedDocumentRevision: null);

        CaptureAnalysisStoreWriteResult result = await context.Coordinator.TryRegisterSourceAsync(
            context.Registration,
            initial.Snapshot!.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, result.Status);
        Assert.IsFalse(result.Snapshot!.Record.TryGetAnalysis(
            AnalysisCapabilities.MediaPropertiesV1.Id,
            out _));
        context.MetadataStore.Dispose();
    }

    [TestMethod]
    public async Task Register_WithCapabilitySelection_ShouldPreserveOtherCapabilityResults()
    {
        AnalyzerIdentity currentPropertiesProducer = CreateAnalyzerIdentity("2");
        AnalyzerIdentity currentOcrProducer = new(
            "windows-ocr",
            "windows",
            modelId: null,
            modelVersion: null,
            adapterVersion: "1",
            runtimeId: null,
            runtimeVersion: null,
            packageVersion: null,
            configurationFingerprint: null);
        var recipe = new CaptureAnalysisRecipe(
            new AnalysisRecipeId("capture-memory-image"),
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Image,
            [
                new RecipeCapability(
                    AnalysisCapabilities.MediaPropertiesV1,
                    RecipeCapabilityRequirement.Required),
                new RecipeCapability(
                    AnalysisCapabilities.OcrDocumentV1,
                    RecipeCapabilityRequirement.Optional),
            ]);
        TestContext context = CreateContext(
            authorizedCalls: int.MaxValue,
            sourceGeneration: 7,
            analyzers:
            [
                new StubAnalyzer(CreateDescriptor(
                    AnalysisCapabilities.MediaPropertiesV1,
                    currentPropertiesProducer)),
                new StubAnalyzer(CreateDescriptor(
                    AnalysisCapabilities.OcrDocumentV1,
                    currentOcrProducer)),
            ],
            recipe,
            capabilityIds: [AnalysisCapabilities.OcrDocumentV1.Id]);
        AnalyzerIdentity oldPropertiesProducer = CreateAnalyzerIdentity("1");
        var canonical = new CanonicalCapabilityResult(
            context.Registration.Preconditions.CaptureId,
            context.Registration.Preconditions.SourceRevision,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 100)),
            oldPropertiesProducer,
            ProcessingBoundary.OnDevice,
            CapturedAtUtc.AddMinutes(1));
        var record = new CaptureAnalysisRecord(
            context.Registration.Preconditions.CaptureId,
            CaptureMediaKind.Image,
            CapturedAtUtc,
            context.Registration.Preconditions.SourceRevision,
            context.Registration.Recipe,
            [new CapabilityAnalysis(AnalysisCapabilities.MediaPropertiesV1, canonical, null)]);
        CaptureAnalysisStoreWriteResult initial = await context.MetadataStore.TryWriteAsync(
            record,
            expectedDocumentRevision: null);

        CaptureAnalysisStoreWriteResult result = await context.Coordinator.TryRegisterSourceAsync(
            context.Registration,
            initial.Snapshot!.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, result.Status);
        Assert.IsTrue(result.Snapshot!.Record.TryGetAnalysis(
            AnalysisCapabilities.MediaPropertiesV1.Id,
            out CapabilityAnalysis? preserved));
        Assert.IsTrue(preserved!.CanonicalResult!.IsEquivalentTo(canonical));
        context.MetadataStore.Dispose();
    }

    [TestMethod]
    public async Task CommitResult_ShouldPersistThenRecognizeAlreadyCurrentPayload()
    {
        AnalyzerIdentity producer = CreateAnalyzerIdentity("2");
        var descriptor = CreateDescriptor(producer);
        TestContext context = CreateContext(
            authorizedCalls: int.MaxValue,
            sourceGeneration: 7,
            [new StubAnalyzer(descriptor)]);
        CaptureAnalysisStoreWriteResult registered = await context.Coordinator.TryRegisterSourceAsync(
            context.Registration,
            expectedDocumentRevision: null);
        var token = new AnalysisCommitToken(
            context.Registration.Preconditions,
            AnalysisCapabilities.MediaPropertiesV1,
            producer.Revision);
        var result = new CanonicalCapabilityResult(
            context.Registration.Preconditions.CaptureId,
            context.Registration.Preconditions.SourceRevision,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 100)),
            producer,
            ProcessingBoundary.OnDevice,
            CapturedAtUtc.AddMinutes(1));

        CaptureAnalysisStoreWriteResult committed = await context.Coordinator.TryCommitCapabilityAsync(
            token,
            result,
            registered.Snapshot!.DocumentRevision);
        CaptureAnalysisStoreWriteResult alreadyCurrent = await context.Coordinator
            .TryCommitCapabilityAsync(
                token,
                result,
                committed.Snapshot!.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, committed.Status);
        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, alreadyCurrent.Status);
        Assert.AreEqual(committed.Snapshot.DocumentRevision, alreadyCurrent.Snapshot!.DocumentRevision);
        Assert.IsTrue(alreadyCurrent.Snapshot.Record.TryGetAnalysis(
            AnalysisCapabilities.MediaPropertiesV1.Id,
            out CapabilityAnalysis? analysis));
        Assert.IsTrue(analysis!.CanonicalResult!.IsEquivalentTo(result));
        context.MetadataStore.Dispose();
    }

    [TestMethod]
    public async Task CommitOutcome_ShouldPersistBoundedTerminalFailure()
    {
        AnalyzerIdentity producer = CreateAnalyzerIdentity("2");
        TestContext context = CreateContext(
            authorizedCalls: int.MaxValue,
            sourceGeneration: 7,
            [new StubAnalyzer(CreateDescriptor(producer))]);
        CaptureAnalysisStoreWriteResult registered = await context.Coordinator.TryRegisterSourceAsync(
            context.Registration,
            expectedDocumentRevision: null);
        var token = new AnalysisCommitToken(
            context.Registration.Preconditions,
            AnalysisCapabilities.MediaPropertiesV1,
            producer.Revision);
        var failure = new AnalysisFailure(
            AnalysisFailureCode.UnsupportedMedia,
            AnalysisFailureDisposition.Terminal);
        var outcome = new CapabilityOutcome(
            context.Registration.Preconditions.CaptureId,
            context.Registration.Preconditions.SourceRevision,
            AnalysisCapabilities.MediaPropertiesV1,
            producer,
            ProcessingBoundary.OnDevice,
            CapabilityOutcomeState.Unsupported,
            failure,
            CapturedAtUtc.AddMinutes(1));

        CaptureAnalysisStoreWriteResult committed = await context.Coordinator.TryCommitCapabilityAsync(
            token,
            outcome,
            registered.Snapshot!.DocumentRevision);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.Succeeded, committed.Status);
        Assert.IsTrue(committed.Snapshot!.Record.TryGetAnalysis(
            AnalysisCapabilities.MediaPropertiesV1.Id,
            out CapabilityAnalysis? analysis));
        Assert.AreEqual(outcome, analysis!.LatestOutcome);
        context.MetadataStore.Dispose();
    }

    [TestMethod]
    public async Task Commit_ShouldRejectUnknownProducerAndMissingMetadata()
    {
        AnalyzerIdentity producer = CreateAnalyzerIdentity("2");
        TestContext noProducer = CreateContext(
            authorizedCalls: int.MaxValue,
            sourceGeneration: 7);
        var token = new AnalysisCommitToken(
            noProducer.Registration.Preconditions,
            AnalysisCapabilities.MediaPropertiesV1,
            producer.Revision);
        var result = new CanonicalCapabilityResult(
            noProducer.Registration.Preconditions.CaptureId,
            noProducer.Registration.Preconditions.SourceRevision,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 100)),
            producer,
            ProcessingBoundary.OnDevice,
            CapturedAtUtc.AddMinutes(1));

        CaptureAnalysisStoreWriteResult unknownProducer = await noProducer.Coordinator
            .TryCommitCapabilityAsync(token, result, expectedDocumentRevision: 1);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.StaleCommit, unknownProducer.Status);
        noProducer.MetadataStore.Dispose();

        TestContext missingMetadata = CreateContext(
            authorizedCalls: int.MaxValue,
            sourceGeneration: 7,
            [new StubAnalyzer(CreateDescriptor(producer))]);
        var missingToken = new AnalysisCommitToken(
            missingMetadata.Registration.Preconditions,
            AnalysisCapabilities.MediaPropertiesV1,
            producer.Revision);
        var missingResult = new CanonicalCapabilityResult(
            missingMetadata.Registration.Preconditions.CaptureId,
            missingMetadata.Registration.Preconditions.SourceRevision,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 100)),
            producer,
            ProcessingBoundary.OnDevice,
            CapturedAtUtc.AddMinutes(1));

        CaptureAnalysisStoreWriteResult notFound = await missingMetadata.Coordinator
            .TryCommitCapabilityAsync(missingToken, missingResult, expectedDocumentRevision: 1);

        Assert.AreEqual(CaptureAnalysisStoreWriteStatus.NotFound, notFound.Status);
        missingMetadata.MetadataStore.Dispose();
    }

    private static CaptureAnalyzerDescriptor CreateDescriptor(AnalyzerIdentity producer) =>
        CreateDescriptor(AnalysisCapabilities.MediaPropertiesV1, producer);

    private static CaptureAnalyzerDescriptor CreateDescriptor(
        CapabilityDefinition capability,
        AnalyzerIdentity producer) => new(
        capability,
        producer,
        [CaptureMediaKind.Image],
        ProcessingBoundary.OnDevice,
        CaptureAnalyzerDataKind.None,
        CaptureAnalyzerRequirement.None,
        CaptureAnalyzerWorkloadClass.Lightweight,
        maximumSourceBytes: null,
        qualityTier: 1);

    private static TestContext CreateContext(
        int authorizedCalls,
        long sourceGeneration,
        IEnumerable<ICaptureAnalyzer>? analyzers = null,
        CaptureAnalysisRecipe? recipe = null,
        IEnumerable<AnalysisCapabilityId>? capabilityIds = null)
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
        recipe ??= new CaptureAnalysisRecipe(
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
            recipe,
            capabilityIds);
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
            new CaptureAnalyzerCatalog(analyzers ?? []),
            metadata);
        return new(coordinator, registration, sourceVerifier, metadata);
    }

    private sealed record TestContext(
        CaptureAnalysisMutationCoordinator Coordinator,
        CaptureAnalysisSourceRegistration Registration,
        RecordingSourceVerifier SourceVerifier,
        LocalCaptureAnalysisStore MetadataStore);

    private static AnalyzerIdentity CreateAnalyzerIdentity(string adapterVersion) => new(
        "windows-media-properties",
        "windows",
        modelId: null,
        modelVersion: null,
        adapterVersion,
        runtimeId: null,
        runtimeVersion: null,
        packageVersion: null,
        configurationFingerprint: null);

    private sealed class StubAnalyzer(CaptureAnalyzerDescriptor descriptor) : ICaptureAnalyzer
    {
        public CaptureAnalyzerDescriptor Descriptor => descriptor;

        public ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
            CaptureAnalyzerAvailabilityRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CaptureAnalyzerAvailability.Available);

        public Task<CaptureAnalyzerOutput> AnalyzeAsync(
            CaptureAnalysisRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

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
        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) => sequence < 7
            ? [new CaptureAssetChange(
                7,
                asset.Id,
                lifecycleRevision: 1,
                CaptureAssetChangeType.Finalized,
                CapturedAtUtc)]
            : [];
        public long GetLatestChangeSequence() => 7;
        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset added) => throw new NotSupportedException();
        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryUpdate(CaptureAsset updated, long expectedLifecycleRevision, CaptureAssetChangeType changeType) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryForget(CaptureId captureId, long expectedLifecycleRevision) => throw new NotSupportedException();
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
