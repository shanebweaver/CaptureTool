using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Analysis.Intake;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Intake;

[TestClass]
public sealed class CaptureAnalysisIntakeServiceTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task DisabledFeature_ShouldAdvanceContentFreeCheckpointAndPreserveEligibleEnrollment()
    {
        CaptureAsset beforeWatermark = CreateAsset(CaptureFileType.Image);
        CaptureAsset eligible = CreateAsset(CaptureFileType.Image);
        CaptureAsset audio = CreateAsset(CaptureFileType.Audio);
        var context = new TestContext(
            featureEnabled: false,
            GrantFutureCaptures(currentSequence: 1),
            [beforeWatermark, eligible, audio],
            [
                CreateChange(1, beforeWatermark, CaptureAssetChangeType.Finalized),
                CreateChange(2, eligible, CaptureAssetChangeType.Finalized),
                CreateChange(3, audio, CaptureAssetChangeType.Finalized),
            ]);

        await context.Service.ConsumePendingChangesAsync();

        Assert.AreEqual(3, context.Control.Snapshot.State.CaptureChangeCheckpoint);
        CaptureAnalysisEnrollment enrollment = context.Control.Snapshot.State.Enrollments.Single();
        Assert.AreEqual(eligible.Id, enrollment.CaptureId);
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Enrolled, enrollment.State);
        Assert.AreEqual(
            CaptureAnalysisRecipeDefaults.CaptureMemoryImageRecipeId,
            enrollment.RequestedRecipeId!.Value.Value);
        Assert.IsEmpty(context.Scheduler.Requests);
        context.FileSystem.Verify(
            fileSystem => fileSystem.FileExists(It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task DisabledFeature_ShouldStillRetryDurableLifecycleCleanupOnStartup()
    {
        var cleanup = new RecordingCleanupCoordinator();
        var context = new TestContext(
            featureEnabled: false,
            CaptureAnalysisPolicy.Unknown,
            assets: [],
            changes: [],
            cleanup: cleanup);

        await context.Service.ReconcileStartupAsync();
        await context.Service.ConsumePendingChangesAsync();

        Assert.AreEqual(2, cleanup.ReconcileCalls);
        Assert.IsEmpty(context.Scheduler.Requests);
    }

    [TestMethod]
    public async Task FailedScheduling_ShouldLeaveCheckpointForIdempotentRetry()
    {
        CaptureAsset asset = CreateAsset(CaptureFileType.Image);
        CaptureAssetChange finalized = CreateChange(1, asset, CaptureAssetChangeType.Finalized);
        var context = new TestContext(
            featureEnabled: true,
            GrantFutureCaptures(currentSequence: 0),
            [asset],
            [finalized]);
        context.Scheduler.Results.Enqueue(new(CaptureAnalysisScheduleStatus.Unavailable));
        context.Scheduler.Results.Enqueue(new(CaptureAnalysisScheduleStatus.Scheduled, 3));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => context.Service.ConsumePendingChangesAsync());
        Assert.AreEqual(0, context.Control.Snapshot.State.CaptureChangeCheckpoint);

        await context.Service.ConsumePendingChangesAsync();

        Assert.AreEqual(1, context.Control.Snapshot.State.CaptureChangeCheckpoint);
        Assert.HasCount(2, context.Scheduler.Requests);
        Assert.AreEqual(
            CaptureAnalysisAdmissionKind.FutureCapture,
            context.Scheduler.Requests[1].Admission.Kind);
    }

    [TestMethod]
    public async Task ReenabledFeature_ShouldAuditContentFreeEnrollmentsWithoutReplayingPreWatermarkAssets()
    {
        CaptureAsset beforeWatermark = CreateAsset(CaptureFileType.Image);
        CaptureAsset eligible = CreateAsset(CaptureFileType.Image);
        var context = new TestContext(
            featureEnabled: false,
            GrantFutureCaptures(currentSequence: 1),
            [beforeWatermark, eligible],
            [
                CreateChange(1, beforeWatermark, CaptureAssetChangeType.Finalized),
                CreateChange(2, eligible, CaptureAssetChangeType.Finalized),
            ]);
        await context.Service.ConsumePendingChangesAsync();

        context.Feature.IsCaptureAnalysisEnabled = true;
        await context.Service.ConsumePendingChangesAsync();

        Assert.HasCount(1, context.Scheduler.Requests);
        Assert.AreEqual(eligible.Id, context.Scheduler.Requests[0].Admission.CaptureId);
        Assert.AreEqual(2, context.Control.Snapshot.State.CaptureChangeCheckpoint);
    }

    [TestMethod]
    public async Task StartupReconciliation_ShouldRepairStaleRecipeAndScheduleEnrolledCapture()
    {
        CaptureAsset asset = CreateAsset(CaptureFileType.Image);
        CaptureAssetChange finalized = CreateChange(1, asset, CaptureAssetChangeType.Finalized);
        var staleEnrollment = new CaptureAnalysisEnrollment(
            asset.Id,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            assetFinalizationSequence: 1,
            new AnalysisRecipeId("legacy-capture-memory-image"),
            new AnalysisRecipeVersion(1));
        var context = new TestContext(
            featureEnabled: true,
            GrantFutureCaptures(currentSequence: 0),
            [asset],
            [finalized],
            [staleEnrollment],
            captureChangeCheckpoint: 1);

        await context.Service.ReconcileStartupAsync();
        await context.Service.ReconcileStartupAsync();

        CaptureAnalysisEnrollment repaired = context.Control.Snapshot.State.Enrollments.Single();
        Assert.AreEqual(2, repaired.EnrollmentGeneration);
        Assert.AreEqual(
            CaptureAnalysisRecipeDefaults.CaptureMemoryImageRecipeId,
            repaired.RequestedRecipeId!.Value.Value);
        Assert.HasCount(1, context.Scheduler.Requests);
    }

    [TestMethod]
    public async Task MissingSource_ShouldCommitTombstoneBeforeCancellingJobsAndDeletingAsset()
    {
        CaptureAsset asset = CreateAsset(CaptureFileType.Image);
        CaptureAssetChange finalized = CreateChange(1, asset, CaptureAssetChangeType.Finalized);
        var events = new List<string>();
        var context = new TestContext(
            featureEnabled: true,
            GrantFutureCaptures(currentSequence: 0),
            [asset],
            [finalized],
            [CreateEnrollment(asset.Id, 1)],
            captureChangeCheckpoint: 1,
            events);
        context.FileSystem
            .Setup(fileSystem => fileSystem.FileExists(asset.RetainedSourcePath))
            .Returns(false);
        context.Jobs
            .Setup(store => store.CancelCaptureAsync(
                asset.Id,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => events.Add("cancel-jobs"))
            .Returns(new ValueTask<int>(0));

        await context.Service.ReconcileStartupAsync();

        CaptureAnalysisEnrollment tombstone = context.Control.Snapshot.State.Enrollments.Single();
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Excluded, tombstone.State);
        Assert.AreEqual(CaptureAnalysisExclusionReason.MissingSource, tombstone.ExclusionReason);
        CollectionAssert.AreEqual(
            new[] { "control-tombstone", "cancel-jobs", "delete-asset" },
            events);
    }

    [TestMethod]
    public async Task PreferredLocationChange_ShouldRefreshProjectionWithoutReadingOrSchedulingSource()
    {
        CaptureAsset asset = CreateAsset(CaptureFileType.Image);
        CaptureAssetChange finalized = CreateChange(1, asset, CaptureAssetChangeType.Finalized);
        CaptureAssetChange preferred = new(
            sequence: 2,
            asset.Id,
            lifecycleRevision: 2,
            CaptureAssetChangeType.PreferredLocationChanged,
            CapturedAtUtc.AddSeconds(2));
        var context = new TestContext(
            featureEnabled: true,
            GrantFutureCaptures(currentSequence: 0),
            [asset],
            [finalized],
            [CreateEnrollment(asset.Id, 1)],
            captureChangeCheckpoint: 1);

        await context.Service.ReconcileStartupAsync();
        context.Scheduler.Requests.Clear();
        context.FileSystem.Invocations.Clear();
        context.Assets.AddChange(preferred);

        await context.Service.ConsumePendingChangesAsync();

        CollectionAssert.AreEqual(new[] { asset.Id }, context.Projection.CaptureIds);
        Assert.IsEmpty(context.Scheduler.Requests);
        context.FileSystem.Verify(
            fileSystem => fileSystem.FileExists(It.IsAny<string>()),
            Times.Never);
        Assert.AreEqual(2, context.Control.Snapshot.State.CaptureChangeCheckpoint);
    }

    [TestMethod]
    public async Task Backfill_ShouldBeCancellableResumableBoundedAndCapturedOriginOnly()
    {
        CaptureAsset first = CreateAsset(CaptureFileType.Image);
        CaptureAsset second = CreateAsset(CaptureFileType.Image);
        CaptureAsset imported = CreateAsset(
            CaptureFileType.Image,
            CaptureSourceOwnership.LegacyExternal);
        CaptureAnalysisPolicy policy = GrantFutureCaptures(currentSequence: 3)
            .AuthorizeExistingCaptureBackfill(currentSequence: 3);
        var context = new TestContext(
            featureEnabled: true,
            policy,
            [first, second, imported],
            [
                CreateChange(1, first, CaptureAssetChangeType.Finalized),
                CreateChange(2, second, CaptureAssetChangeType.Finalized),
                CreateChange(3, imported, CaptureAssetChangeType.Finalized),
            ]);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<CaptureAnalysisBackfillProgress>(value =>
        {
            if (value.Checkpoint == 1)
            {
                cancellation.Cancel();
            }
        });

        CaptureAnalysisBackfillRunResult cancelled = await context.Service.RunAsync(
            progress,
            cancellation.Token);

        Assert.AreEqual(CaptureAnalysisBackfillRunStatus.Cancelled, cancelled.Status);
        Assert.AreEqual(1, context.Control.Snapshot.State.BackfillCheckpoint);
        Assert.AreEqual(1, cancelled.Progress.ScheduledCaptureCount);
        Assert.HasCount(1, context.Scheduler.Requests);

        CaptureAnalysisBackfillRunResult resumed = await context.Service.RunAsync();

        Assert.AreEqual(CaptureAnalysisBackfillRunStatus.Completed, resumed.Status);
        Assert.AreEqual(3, resumed.Progress.Checkpoint);
        Assert.HasCount(2, context.Scheduler.Requests);
        Assert.IsTrue(context.Scheduler.Requests.All(request =>
            request.Admission.Kind == CaptureAnalysisAdmissionKind.ExistingCaptureBackfill));
        Assert.IsFalse(context.Scheduler.Requests.Any(request =>
            request.Admission.CaptureId == imported.Id));

        CaptureAnalysisBackfillRunResult repeated = await context.Service.RunAsync();
        Assert.AreEqual(CaptureAnalysisBackfillRunStatus.AlreadyCompleted, repeated.Status);
        Assert.HasCount(2, context.Scheduler.Requests);
    }

    [TestMethod]
    public async Task Backfill_ShouldRequireSeparateExplicitAuthorization()
    {
        CaptureAsset asset = CreateAsset(CaptureFileType.Image);
        var context = new TestContext(
            featureEnabled: true,
            GrantFutureCaptures(currentSequence: 1),
            [asset],
            [CreateChange(1, asset, CaptureAssetChangeType.Finalized)]);

        CaptureAnalysisBackfillRunResult result = await context.Service.RunAsync();

        Assert.AreEqual(CaptureAnalysisBackfillRunStatus.NotAuthorized, result.Status);
        Assert.IsEmpty(context.Scheduler.Requests);
        context.FileSystem.Verify(
            fileSystem => fileSystem.FileExists(It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task EmptyAuthorizedBackfill_ShouldCompleteOnItsFirstRun()
    {
        CaptureAnalysisPolicy policy = GrantFutureCaptures(currentSequence: 0)
            .AuthorizeExistingCaptureBackfill(currentSequence: 0);
        var context = new TestContext(
            featureEnabled: true,
            policy,
            [],
            []);

        CaptureAnalysisBackfillRunResult result = await context.Service.RunAsync();

        Assert.AreEqual(CaptureAnalysisBackfillRunStatus.Completed, result.Status);
        Assert.AreEqual(CaptureAnalysisBackfillState.Completed, context.Control.Snapshot.State.BackfillState);
    }

    private static CaptureAnalysisPolicy GrantFutureCaptures(long currentSequence) =>
        CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
            currentSequence);

    private static CaptureAsset CreateAsset(
        CaptureFileType mediaType,
        CaptureSourceOwnership sourceOwnership = CaptureSourceOwnership.AppOwned)
    {
        CaptureId id = CaptureId.New();
        string extension = mediaType == CaptureFileType.Image ? ".png" : ".bin";
        return new(
            id,
            mediaType,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), id + extension)),
            sourceOwnership,
            CapturedAtUtc);
    }

    private static CaptureAssetChange CreateChange(
        long sequence,
        CaptureAsset asset,
        CaptureAssetChangeType changeType) => new(
            sequence,
            asset.Id,
            changeType == CaptureAssetChangeType.Finalized ? 1 : asset.LifecycleRevision,
            changeType,
            CapturedAtUtc.AddSeconds(sequence));

    private static CaptureAnalysisEnrollment CreateEnrollment(
        CaptureId captureId,
        long finalizationSequence)
    {
        CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe();
        return new(
            captureId,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            finalizationSequence,
            recipe.Id,
            recipe.Version);
    }

    private sealed class TestContext
    {
        public TestContext(
            bool featureEnabled,
            CaptureAnalysisPolicy policy,
            IReadOnlyList<CaptureAsset> assets,
            IReadOnlyList<CaptureAssetChange> changes,
            IReadOnlyList<CaptureAnalysisEnrollment>? enrollments = null,
            long captureChangeCheckpoint = 0,
            List<string>? events = null,
            ICaptureAnalysisCleanupCoordinator? cleanup = null)
        {
            Events = events ?? [];
            Control = new InMemoryControlStore(
                new CaptureAnalysisControlState(policy, enrollments ?? [], captureChangeCheckpoint),
                Events);
            Assets = new InMemoryAssetCatalog(assets, changes, Events);
            Feature = new TestFeatureAvailability(featureEnabled);
            Policy = new TestPolicyService(Control, Feature);
            Scheduler = new RecordingScheduler();
            Jobs = new Mock<ICaptureAnalysisJobStore>(MockBehavior.Loose);
            Projection = new RecordingProjectionRefresher();
            FileSystem = new Mock<IFileSystem>(MockBehavior.Loose);
            FileSystem.Setup(fileSystem => fileSystem.FileExists(It.IsAny<string>())).Returns(true);
            Service = new CaptureAnalysisIntakeService(
                new CaptureAssetChangeReader(Assets),
                Assets,
                Control,
                Policy,
                Scheduler,
                Jobs.Object,
                Projection,
                Feature,
                FileSystem.Object,
                cleanup);
        }

        public List<string> Events { get; }
        public InMemoryControlStore Control { get; }
        public InMemoryAssetCatalog Assets { get; }
        public TestFeatureAvailability Feature { get; }
        public TestPolicyService Policy { get; }
        public RecordingScheduler Scheduler { get; }
        public Mock<ICaptureAnalysisJobStore> Jobs { get; }
        public RecordingProjectionRefresher Projection { get; }
        public Mock<IFileSystem> FileSystem { get; }
        public CaptureAnalysisIntakeService Service { get; }
    }

    private sealed class InMemoryControlStore(
        CaptureAnalysisControlState initial,
        List<string> events) : ICaptureAnalysisControlStore
    {
        public CaptureAnalysisControlSnapshot Snapshot { get; private set; } = new(1, initial);

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
                    CaptureAnalysisControlWriteStatus.Conflict,
                    Snapshot));
            }

            bool addedTombstone = state.Enrollments.Any(enrollment =>
                enrollment.State == CaptureAnalysisEnrollmentState.Excluded &&
                !Snapshot.State.Enrollments.Any(existing =>
                    existing.CaptureId == enrollment.CaptureId &&
                    existing.State == CaptureAnalysisEnrollmentState.Excluded));
            if (addedTombstone)
            {
                events.Add("control-tombstone");
            }

            Snapshot = new(Snapshot.DocumentRevision + 1, state);
            return ValueTask.FromResult(new CaptureAnalysisControlWriteResult(
                CaptureAnalysisControlWriteStatus.Succeeded,
                Snapshot));
        }
    }

    private sealed class InMemoryAssetCatalog : ICaptureAssetCatalog
    {
        private readonly Dictionary<CaptureId, CaptureAsset> _assets;
        private readonly List<CaptureAssetChange> _changes;
        private readonly List<string> _events;

        public InMemoryAssetCatalog(
            IReadOnlyList<CaptureAsset> assets,
            IReadOnlyList<CaptureAssetChange> changes,
            List<string> events)
        {
            _assets = assets.ToDictionary(asset => asset.Id);
            _changes = [.. changes.OrderBy(change => change.Sequence)];
            _events = events;
        }

        public IReadOnlyList<CaptureAsset> GetAssets() => [.. _assets.Values];
        public CaptureAsset? Get(CaptureId captureId) => _assets.GetValueOrDefault(captureId);
        public CaptureAsset? FindByPath(string filePath) => _assets.Values.FirstOrDefault(
            asset => string.Equals(asset.RetainedSourcePath, filePath, StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<CaptureAssetChange> GetChangesAfter(long sequence) =>
            _changes.Where(change => change.Sequence > sequence).ToArray();
        public long GetLatestChangeSequence() => _changes.Count == 0 ? 0 : _changes[^1].Sequence;
        public void AddChange(CaptureAssetChange change) => _changes.Add(change);
        public CaptureAssetCatalogWriteResult TryAdd(CaptureAsset asset) => throw new NotSupportedException();
        public IReadOnlyList<CaptureAssetCatalogWriteResult> TryAddRange(IReadOnlyList<CaptureAsset> assets) => throw new NotSupportedException();

        public CaptureAssetCatalogWriteResult TryUpdate(
            CaptureAsset asset,
            long expectedLifecycleRevision,
            CaptureAssetChangeType changeType)
        {
            CaptureAsset? current = Get(asset.Id);
            if (current == null || current.LifecycleRevision != expectedLifecycleRevision)
            {
                return CaptureAssetCatalogWriteResult.Failed;
            }

            _events.Add("delete-asset");
            _assets[asset.Id] = asset;
            long sequence = GetLatestChangeSequence() + 1;
            _changes.Add(new(
                sequence,
                asset.Id,
                asset.LifecycleRevision,
                changeType,
                CapturedAtUtc.AddMinutes(1)));
            return CaptureAssetCatalogWriteResult.Committed(asset, sequence);
        }

        public CaptureAssetCatalogWriteResult TryForget(
            CaptureId captureId,
            long expectedLifecycleRevision) => throw new NotSupportedException();
    }

    private sealed class TestFeatureAvailability(bool enabled) : ICaptureAnalysisFeatureAvailability
    {
        public bool IsCaptureAnalysisEnabled { get; set; } = enabled;
        public long ResolutionPolicyRevision => 1;
        public bool IsProviderEnabled(string providerId) => true;
        public bool IsAnalyzerEnabled(AnalyzerIdentity analyzer) => true;
    }

    private sealed class TestPolicyService(
        InMemoryControlStore control,
        TestFeatureAvailability feature) : ICaptureAnalysisPolicyService
    {
        public ValueTask<CaptureAnalysisPolicySnapshot> GetCurrentAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(
                new CaptureAnalysisPolicySnapshot(
                    feature.IsCaptureAnalysisEnabled
                        ? CaptureAnalysisPolicySnapshotStatus.Available
                        : CaptureAnalysisPolicySnapshotStatus.FeatureDisabled,
                    CaptureAnalysisConsentState.Granted,
                    control.Snapshot));

        public ValueTask<CaptureAnalysisAdmissionDecision> AuthorizeAdmissionAsync(
            CaptureAnalysisAdmissionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<CaptureAnalysisAuthorizationDecision> AuthorizeAsync(
            CaptureAnalysisAuthorizationRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingScheduler : ICaptureAnalysisScheduler
    {
        public List<CaptureAnalysisScheduleRequest> Requests { get; } = [];
        public Queue<CaptureAnalysisScheduleResult> Results { get; } = [];

        public ValueTask<CaptureAnalysisScheduleResult> ScheduleAsync(
            CaptureAnalysisScheduleRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(Results.Count == 0
                ? new CaptureAnalysisScheduleResult(CaptureAnalysisScheduleStatus.Scheduled, 3)
                : Results.Dequeue());
        }
    }

    private sealed class RecordingProjectionRefresher : ICaptureAnalysisProjectionRefresher
    {
        public List<CaptureId> CaptureIds { get; } = [];

        public ValueTask RefreshAsync(
            CaptureId captureId,
            CancellationToken cancellationToken = default)
        {
            CaptureIds.Add(captureId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCleanupCoordinator : ICaptureAnalysisCleanupCoordinator
    {
        public int ReconcileCalls { get; private set; }

        public ValueTask<bool> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            ReconcileCalls++;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> ReconcileCaptureAsync(
            CaptureId captureId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
