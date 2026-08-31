using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Analysis.Analyzers;
using CaptureTool.Application.Analysis.Intake;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Application.Analysis.Memory;
using CaptureTool.Application.Analysis.Orchestration;
using CaptureTool.Application.Analysis.Policy;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Orchestration;

[TestClass]
public sealed class CaptureAnalysisSchedulerTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Schedule_ShouldPersistEveryIntentBeforeBestEffortWake()
    {
        CaptureId captureId = CaptureId.New();
        string path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"{captureId}.png"));
        var asset = new CaptureAsset(
            captureId,
            CaptureFileType.Image,
            path,
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc);
        var finalization = new CaptureAssetChange(
            sequence: 11,
            captureId,
            lifecycleRevision: 1,
            CaptureAssetChangeType.Finalized,
            CapturedAtUtc.AddSeconds(1));
        CaptureAnalysisAuthorizationScope scope = CaptureAnalysisPolicyDefaults.CreateAuthorizationScope();
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(scope, currentSequence: 10);
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
        var enrollment = new CaptureAnalysisEnrollment(
            captureId,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            assetFinalizationSequence: finalization.Sequence,
            recipe.Id,
            recipe.Version);
        var control = new CaptureAnalysisControlSnapshot(
            documentRevision: 1,
            new CaptureAnalysisControlState(policy, [enrollment]));
        var assets = new StubCaptureAssetCatalog(asset, finalization);
        var policyService = new StubPolicyService(control, scope);
        var source = new StubVerifiedSource(captureId);
        var sourceVerifier = new StubSourceVerifier(source);
        var metadata = new StubMetadataStore();
        var mutation = new StubMutationCoordinator(metadata, asset, recipe);
        var jobs = new RecordingJobStore();
        var wake = new RecordingWakeSignal(jobs, expectedIntentCount: 2);
        var feature = new StubFeatureAvailability();
        var scheduler = new CaptureAnalysisScheduler(
            policyService,
            new StubControlStore(control),
            sourceVerifier,
            mutation,
            metadata,
            jobs,
            wake,
            feature,
            new CaptureAnalyzerCatalog([]),
            assets,
            new StubClock(CapturedAtUtc.AddMinutes(1)));
        var request = new CaptureAnalysisScheduleRequest(
            new CaptureAnalysisAdmissionRequest(
                finalization,
                CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                CaptureAnalysisAdmissionKind.FutureCapture),
            recipe,
            ProcessingBoundary.OnDevice);

        CaptureAnalysisScheduleResult result = await scheduler.ScheduleAsync(request);

        Assert.AreEqual(CaptureAnalysisScheduleStatus.Scheduled, result.Status);
        Assert.AreEqual(2, result.DurableIntentCount);
        Assert.HasCount(2, jobs.Keys);
        Assert.HasCount(2, jobs.EnqueuedAtUtc);
        Assert.AreEqual(CapturedAtUtc.AddMinutes(1), jobs.EnqueuedAtUtc[0]);
        Assert.AreEqual(CapturedAtUtc.AddMinutes(1).AddTicks(1), jobs.EnqueuedAtUtc[1]);
        Assert.IsTrue(wake.WasCalledAfterAllIntents);
        Assert.AreEqual(1, sourceVerifier.OpenCount);
    }

    [TestMethod]
    public async Task Schedule_ShouldRequeueCompletedIntentWhenProducerRevisionIsStale()
    {
        CaptureId captureId = CaptureId.New();
        string path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"{captureId}.png"));
        var asset = new CaptureAsset(
            captureId,
            CaptureFileType.Image,
            path,
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc);
        var finalization = new CaptureAssetChange(
            sequence: 11,
            captureId,
            lifecycleRevision: 1,
            CaptureAssetChangeType.Finalized,
            CapturedAtUtc.AddSeconds(1));
        CaptureAnalysisAuthorizationScope scope = CaptureAnalysisPolicyDefaults.CreateAuthorizationScope();
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(scope, 10);
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
            1,
            0,
            finalization.Sequence,
            recipe.Id,
            recipe.Version);
        var control = new CaptureAnalysisControlSnapshot(
            1,
            new CaptureAnalysisControlState(policy, [enrollment]));
        var assets = new StubCaptureAssetCatalog(asset, finalization);
        var source = new StubVerifiedSource(captureId);
        var metadata = new StubMetadataStore();
        AnalyzerIdentity oldProducer = CreateAnalyzerIdentity("1");
        var oldResult = new CanonicalCapabilityResult(
            captureId,
            source.SourceRevision,
            new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(100, 100)),
            oldProducer,
            ProcessingBoundary.OnDevice,
            CapturedAtUtc.AddSeconds(3));
        metadata.Snapshot = new(
            1,
            new CaptureAnalysisRecord(
                captureId,
                CaptureMediaKind.Image,
                CapturedAtUtc,
                source.SourceRevision,
                recipe,
                [new CapabilityAnalysis(AnalysisCapabilities.MediaPropertiesV1, oldResult, null)]));
        AnalyzerIdentity currentProducer = CreateAnalyzerIdentity("2");
        var analyzer = new Mock<ICaptureAnalyzer>();
        analyzer.SetupGet(candidate => candidate.Descriptor).Returns(new CaptureAnalyzerDescriptor(
            AnalysisCapabilities.MediaPropertiesV1,
            currentProducer,
            [CaptureMediaKind.Image],
            ProcessingBoundary.OnDevice,
            CaptureAnalyzerDataKind.None,
            CaptureAnalyzerRequirement.None,
            CaptureAnalyzerWorkloadClass.Lightweight,
            maximumSourceBytes: null,
            qualityTier: 1));
        var jobs = new RecordingJobStore
        {
            EnqueueStatus = CaptureAnalysisJobEnqueueStatus.AlreadyExists,
        };
        var scheduler = new CaptureAnalysisScheduler(
            new StubPolicyService(control, scope),
            new StubControlStore(control),
            new StubSourceVerifier(source),
            new StubMutationCoordinator(metadata, asset, recipe),
            metadata,
            jobs,
            new RecordingWakeSignal(jobs, 1),
            new StubFeatureAvailability(),
            new CaptureAnalyzerCatalog([analyzer.Object]),
            assets,
            new StubClock(CapturedAtUtc.AddMinutes(1)));

        CaptureAnalysisScheduleResult result = await scheduler.ScheduleAsync(new(
            new CaptureAnalysisAdmissionRequest(
                finalization,
                CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                CaptureAnalysisAdmissionKind.FutureCapture),
            recipe,
            ProcessingBoundary.OnDevice));

        Assert.AreEqual(CaptureAnalysisScheduleStatus.Scheduled, result.Status);
        Assert.AreEqual(1, jobs.RequeueCount);
    }

    [TestMethod]
    public async Task RepeatedEraseEnableAndBackfill_ShouldScheduleFreshGenerationsAndRestoreSearch()
    {
        CaptureId id = CaptureId.New();
        var asset = new CaptureAsset(id, CaptureFileType.Image,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"{id}.png")),
            CaptureSourceOwnership.AppOwned, CapturedAtUtc);
        var finalization = new CaptureAssetChange(11, id, 1,
            CaptureAssetChangeType.Finalized, CapturedAtUtc);
        var assets = new StubCaptureAssetCatalog(asset, finalization);
        var control = new StubControlStore(new CaptureAnalysisControlSnapshot(1,
            new CaptureAnalysisControlState(CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
                CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(), 10), [])));
        string consent = CaptureAnalysisConsentSettingValues.Granted;
        var settings = new Mock<ISettingsService>();
        settings.Setup(value => value.IsSet(CaptureToolSettings.Settings_CaptureAnalysisConsent)).Returns(true);
        settings.Setup(value => value.Get(CaptureToolSettings.Settings_CaptureAnalysisConsent)).Returns(() => consent);
        settings.Setup(value => value.TrySetAndSaveAsync(CaptureToolSettings.Settings_CaptureAnalysisConsent,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IStringSettingDefinition _, string value, CancellationToken _) =>
            {
                consent = value;
                return SettingsMutationResult.Saved;
            });
        var feature = new StubFeatureAvailability();
        using var policy = new CaptureAnalysisPolicyService(assets, control, feature, settings.Object);
        var metadata = new StubMetadataStore();
        using var projection = new CaptureMemorySearchProjection(metadata, control, assets);
        var cleanup = new ClearingCleanupCoordinator(metadata, projection);
        var commands = new CaptureAnalysisPolicyCommandService(policy, cleanup);
        var jobs = new RecordingJobStore();
        var source = new StubVerifiedSource(id);
        CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe();
        var scheduler = new CaptureAnalysisScheduler(policy, control, new StubSourceVerifier(source),
            new StubMutationCoordinator(metadata, asset, recipe), metadata, jobs,
            new RecordingWakeSignal(jobs, recipe.Capabilities.Count), feature, new CaptureAnalyzerCatalog([]),
            assets, new StubClock(CapturedAtUtc), cleanup);
        var changes = new Mock<ICaptureAssetChangeReader>();
        changes.Setup(value => value.ReadAfterAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long checkpoint, CancellationToken _) => new CaptureAssetChangeBatch(
                checkpoint, 11, 11, checkpoint < 11 ? [finalization] : []));
        using var intake = new CaptureAnalysisIntakeService(changes.Object, assets, control, policy,
            scheduler, jobs, projection, feature, Mock.Of<IFileSystem>(), cleanup);

        for (int cycle = 0; cycle < 4; cycle++)
        {
            if (cycle == 0)
            {
                Assert.AreEqual(CaptureAnalysisScheduleStatus.Scheduled,
                    (await scheduler.ScheduleAsync(new(new CaptureAnalysisAdmissionRequest(finalization,
                        CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                        CaptureAnalysisAdmissionKind.FutureCapture), recipe, ProcessingBoundary.OnDevice))).Status);
            }
            else
            {
                await commands.ApplyConsentDecisionAsync(new CaptureAnalysisConsentResponse(
                    CaptureAnalysisPolicyDefaults.CreateConsentDisclosure(),
                    CaptureAnalysisConsentDecision.GrantedForFutureCaptures), control.Snapshot.DocumentRevision);
                Assert.IsFalse((await policy.AuthorizeAdmissionAsync(new(finalization,
                    CaptureAnalysisPolicyDefaults.CaptureMemorySearchPurpose,
                    CaptureAnalysisAdmissionKind.FutureCapture))).IsAuthorized);
                await commands.AuthorizeExistingCaptureBackfillAsync(control.Snapshot.DocumentRevision);
                CaptureAnalysisBackfillRunResult backfill = await intake.RunAsync();
                Assert.AreEqual(CaptureAnalysisBackfillRunStatus.Completed, backfill.Status);
                Assert.AreEqual(1, backfill.Progress.ScheduledCaptureCount);
            }

            CaptureAnalysisEnrollment enrollment = control.Snapshot.State.Enrollments.Single();
            Assert.AreEqual(CaptureAnalysisEnrollmentState.Enrolled, enrollment.State);
            Assert.AreEqual(cycle * 2 + 1, enrollment.EnrollmentGeneration);
            Assert.AreEqual(cycle, enrollment.TombstoneGeneration);
            Assert.HasCount((cycle + 1) * recipe.Capabilities.Count, jobs.Keys);
            Assert.AreEqual(jobs.Keys.Count, jobs.Keys.Distinct().Count());

            // Simulate fresh model output, then use the real projection/search implementation.
            string recognizedText = $"recovered text cycle {cycle}";
            var result = new CanonicalCapabilityResult(id, source.SourceRevision,
                new OcrDocumentV1(new PixelSize(100, 100), recognizedText, [], []),
                CreateAnalyzerIdentity("1"), ProcessingBoundary.OnDevice, CapturedAtUtc);
            metadata.Snapshot = new CaptureAnalysisStoreSnapshot(2,
                new CaptureAnalysisRecord(id, CaptureMediaKind.Image, CapturedAtUtc,
                    source.SourceRevision, recipe,
                    [new CapabilityAnalysis(AnalysisCapabilities.OcrDocumentV1, result, null)]));
            await projection.RefreshAsync(id);
            Assert.HasCount(1, await projection.SearchAsync(new CaptureMemorySearchRequest(recognizedText, 10)));

            await commands.RevokeAsync(control.Snapshot.DocumentRevision);
            Assert.AreEqual(CaptureAnalysisExclusionReason.MemoryCleared,
                control.Snapshot.State.Enrollments.Single().ExclusionReason);
            Assert.IsNull(metadata.Snapshot);
            Assert.IsEmpty(await projection.SearchAsync(new CaptureMemorySearchRequest(recognizedText, 10)));
        }
    }

    private sealed class ClearingCleanupCoordinator(
        StubMetadataStore metadata, CaptureMemorySearchProjection projection) : ICaptureAnalysisCleanupCoordinator
    {
        public async ValueTask<bool> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            metadata.Snapshot = null;
            await projection.ClearAsync(cancellationToken);
            return true;
        }

        public ValueTask<bool> ReconcileCaptureAsync(CaptureId id,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

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

    private sealed class StubPolicyService(
        CaptureAnalysisControlSnapshot control,
        CaptureAnalysisAuthorizationScope scope) : ICaptureAnalysisPolicyService
    {
        public ValueTask<CaptureAnalysisPolicySnapshot> GetCurrentAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new CaptureAnalysisPolicySnapshot(
                CaptureAnalysisPolicySnapshotStatus.Available,
                CaptureAnalysisConsentState.Granted,
                control));

        public ValueTask<CaptureAnalysisAdmissionDecision> AuthorizeAdmissionAsync(
            CaptureAnalysisAdmissionRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CaptureAnalysisAdmissionDecision.Authorized(
                request,
                control.State.PolicyRevision,
                control.State.ControlGeneration,
                enrollmentGeneration: 1,
                tombstoneGeneration: 0,
                scope));

        public ValueTask<CaptureAnalysisAuthorizationDecision> AuthorizeAsync(
            CaptureAnalysisAuthorizationRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CaptureAnalysisAuthorizationDecision.Authorized(
                request,
                control.State.PolicyRevision,
                control.State.ControlGeneration,
                enrollmentGeneration: 1,
                tombstoneGeneration: 0,
                scope));
    }

    private sealed class StubControlStore(CaptureAnalysisControlSnapshot snapshot) :
        ICaptureAnalysisControlStore
    {
        public CaptureAnalysisControlSnapshot Snapshot { get; private set; } = snapshot;

        public ValueTask<CaptureAnalysisControlSnapshot> GetAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);

        public ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(
            CaptureAnalysisControlState state,
            long expectedDocumentRevision,
            CancellationToken cancellationToken = default)
        {
            if (expectedDocumentRevision != Snapshot.DocumentRevision)
            {
                return ValueTask.FromResult(new CaptureAnalysisControlWriteResult(
                    CaptureAnalysisControlWriteStatus.Conflict, Snapshot));
            }

            Snapshot = new(Snapshot.DocumentRevision + 1, state);
            return ValueTask.FromResult(new CaptureAnalysisControlWriteResult(
                CaptureAnalysisControlWriteStatus.Succeeded, Snapshot));
        }
    }

    private sealed class StubSourceVerifier(IVerifiedCaptureAnalysisSource source) :
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

    private sealed class StubVerifiedSource : IVerifiedCaptureAnalysisSource
    {
        public StubVerifiedSource(CaptureId captureId)
        {
            CaptureId = captureId;
            SourceStamp = new(128, CapturedAtUtc.AddSeconds(2));
            SourceRevision = new(
                SourceStamp.Length,
                SourceStamp.LastWriteTimeUtc,
                ContentFingerprint.Sha256(new string('a', 64)));
        }

        public CaptureId CaptureId { get; }

        public CaptureMediaKind MediaKind => CaptureMediaKind.Image;

        public long CaptureSourceGeneration => 11;

        public ProvisionalSourceStamp SourceStamp { get; }

        public SourceRevision SourceRevision { get; }

        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<Stream>(new MemoryStream(new byte[128]));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubMetadataStore : ICaptureAnalysisStore
    {
        public CaptureAnalysisStoreSnapshot? Snapshot { get; set; }

        public ValueTask<CaptureAnalysisStoreSnapshot?> GetAsync(
            CaptureId captureId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);

        public async IAsyncEnumerable<CaptureAnalysisStoreSnapshot> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            if (Snapshot != null)
            {
                yield return Snapshot;
            }
        }
    }

    private sealed class StubMutationCoordinator(
        StubMetadataStore store,
        CaptureAsset asset,
        CaptureAnalysisRecipe recipe) : ICaptureAnalysisMutationCoordinator
    {
        public ValueTask<CaptureAnalysisStoreWriteResult> TryRegisterSourceAsync(
            CaptureAnalysisSourceRegistration registration,
            long? expectedDocumentRevision,
            CancellationToken cancellationToken = default)
        {
            var record = new CaptureAnalysisRecord(
                registration.Preconditions.CaptureId,
                registration.MediaKind,
                asset.CapturedAtUtc,
                registration.Preconditions.SourceRevision,
                recipe);
            store.Snapshot = new CaptureAnalysisStoreSnapshot(1, record);
            return ValueTask.FromResult(new CaptureAnalysisStoreWriteResult(
                CaptureAnalysisStoreWriteStatus.Succeeded,
                store.Snapshot));
        }

        public ValueTask<CaptureAnalysisStoreWriteResult> TryCommitCapabilityAsync(
            AnalysisCommitToken commitToken,
            CanonicalCapabilityResult result,
            long expectedDocumentRevision,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<CaptureAnalysisStoreWriteResult> TryCommitCapabilityAsync(
            AnalysisCommitToken commitToken,
            CapabilityOutcome outcome,
            long expectedDocumentRevision,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<CaptureAnalysisStoreWriteResult> TryDeleteAsync(
            CaptureAnalysisDeletionToken deletionToken,
            long expectedDocumentRevision,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingJobStore : ICaptureAnalysisJobStore
    {
        public List<CaptureAnalysisJobKey> Keys { get; } = [];

        public List<DateTimeOffset> EnqueuedAtUtc { get; } = [];

        public CaptureAnalysisJobEnqueueStatus EnqueueStatus { get; set; } =
            CaptureAnalysisJobEnqueueStatus.Enqueued;

        public int RequeueCount { get; private set; }

        public ValueTask<CaptureAnalysisJobEnqueueResult> TryEnqueueAsync(
            CaptureAnalysisJobKey key,
            DateTimeOffset enqueuedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Keys.Add(key);
            EnqueuedAtUtc.Add(enqueuedAtUtc);
            return ValueTask.FromResult(new CaptureAnalysisJobEnqueueResult(
                EnqueueStatus,
                new CaptureAnalysisJobIntent(
                    key,
                    EnqueueStatus == CaptureAnalysisJobEnqueueStatus.Enqueued
                        ? CaptureAnalysisJobState.Pending
                        : CaptureAnalysisJobState.Completed,
                    0,
                    enqueuedAtUtc,
                    null,
                    null,
                    [])));
        }

        public ValueTask<CaptureAnalysisJobEnqueueResult> TryRequeueAsync(
            CaptureAnalysisJobKey key,
            DateTimeOffset enqueuedAtUtc,
            CancellationToken cancellationToken = default)
        {
            RequeueCount++;
            return ValueTask.FromResult(new CaptureAnalysisJobEnqueueResult(
                CaptureAnalysisJobEnqueueStatus.Enqueued,
                new CaptureAnalysisJobIntent(
                    key,
                    CaptureAnalysisJobState.Pending,
                    0,
                    enqueuedAtUtc,
                    null,
                    null,
                    [])));
        }

        public ValueTask<CaptureAnalysisJobIntent?> GetAsync(CaptureAnalysisJobKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<CaptureAnalysisJobIntent> ReadAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobLease?> TryLeaseNextDueAsync(DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<DateTimeOffset?> GetNextDueTimeAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> RecoverExpiredLeasesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobMutationResult> TryRenewLeaseAsync(CaptureAnalysisJobLeaseToken leaseToken, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobMutationResult> TryRecordAttemptAsync(CaptureAnalysisJobLeaseToken leaseToken, CaptureAnalyzerAttempt attempt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobMutationResult> TryScheduleRetryAsync(CaptureAnalysisJobLeaseToken leaseToken, AnalysisFailure failure, DateTimeOffset nextAttemptAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobMutationResult> TryWaitForCapabilityAsync(CaptureAnalysisJobLeaseToken leaseToken, AnalysisFailure reason, DateTimeOffset recheckAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobMutationResult> TryCompleteAsync(CaptureAnalysisJobLeaseToken leaseToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobMutationResult> TryFailTerminalAsync(CaptureAnalysisJobLeaseToken leaseToken, AnalysisFailure failure, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> ResumeWaitingForCapabilityAsync(CapabilityDefinition capability, ProcessingBoundary processingBoundary, DateTimeOffset dueAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> ResumeWaitingForDependencyAsync(CaptureId captureId, CapabilityDefinition dependency, DateTimeOffset dueAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CaptureAnalysisJobMutationResult> TryCancelAsync(CaptureAnalysisJobKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> CancelCaptureAsync(CaptureId captureId, long minimumTombstoneGeneration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<int> CancelBeforeControlGenerationAsync(long controlGeneration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingWakeSignal(RecordingJobStore jobs, int expectedIntentCount) :
        ICaptureAnalysisWakeSignal
    {
        public bool WasCalledAfterAllIntents { get; private set; }

        public bool TrySignal()
        {
            WasCalledAfterAllIntents = jobs.Keys.Count == expectedIntentCount;
            return false;
        }
    }

    private sealed class StubCaptureAssetCatalog(
        CaptureAsset asset,
        CaptureAssetChange finalization) : ICaptureAssetCatalog
    {
        public IReadOnlyList<CaptureAsset> GetAssets() => [asset];
        public CaptureAsset? Get(CaptureId captureId) => captureId == asset.Id ? asset : null;
        public CaptureAsset? FindByPath(string filePath) => asset;
        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) => sequence < finalization.Sequence ? [finalization] : [];
        public long GetLatestChangeSequence() => finalization.Sequence;
        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset added) => throw new NotSupportedException();
        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryUpdate(CaptureAsset updated, long expectedLifecycleRevision, CaptureAssetChangeType changeType) => throw new NotSupportedException();
        public CaptureAssetCatalogWriteResult TryForget(CaptureId captureId, long expectedLifecycleRevision) => throw new NotSupportedException();
    }

    private sealed class StubFeatureAvailability : ICaptureAnalysisFeatureAvailability
    {
        public bool IsCaptureAnalysisEnabled => true;
        public long ResolutionPolicyRevision => 1;
        public bool IsProviderEnabled(string providerId) => true;
        public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer) => true;
    }

    private sealed class StubClock(DateTimeOffset now) : IClock
    {
        public DateTime Now => now.LocalDateTime;
        public DateTime UtcNow => now.UtcDateTime;
    }
}
