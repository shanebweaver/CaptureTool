using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Checkpoints;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Analysis.Privacy;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Maintenance;

[TestClass]
public sealed class CaptureAnalysisLifecycleServiceTests
{
    [TestMethod]
    public async Task ExcludeAsync_ShouldCommitTombstoneBeforeCleanup()
    {
        List<string> ordering = [];
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled), ordering);
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 2);
        var cleanup = new TestCleanupCoordinator(ordering);
        using CaptureAnalysisLifecycleService service = CreateService(store, assets, cleanup);
        var request = new CaptureAnalysisExclusionRequest(
            enrolled.CaptureId,
            CaptureAnalysisExclusionKind.UserExcluded);

        CaptureAnalysisExclusionResult result = await service.ExcludeAsync(request);

        Assert.AreEqual(CaptureAnalysisExclusionStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(new[] { "control", "cleanup" }, ordering);
        CaptureAnalysisEnrollment tombstone = store.Snapshot.State.Enrollments.Single();
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Excluded, tombstone.State);
        Assert.AreEqual(CaptureAnalysisExclusionReason.UserExcluded, tombstone.ExclusionReason);
        Assert.AreEqual(2, tombstone.EnrollmentGeneration);
        Assert.AreEqual(1, tombstone.TombstoneGeneration);
        Assert.IsNull(tombstone.RequestedRecipeId);
    }

    [TestMethod]
    [DataRow(CaptureAnalysisExclusionKind.UserExcluded, CaptureAnalysisExclusionReason.UserExcluded)]
    [DataRow(CaptureAnalysisExclusionKind.PrivateCapture, CaptureAnalysisExclusionReason.PrivateCapture)]
    public async Task ExcludeAsync_AfterMemoryClear_ShouldPersistPermanentExclusion(
        CaptureAnalysisExclusionKind kind,
        CaptureAnalysisExclusionReason reason)
    {
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled));
        using CaptureAnalysisLifecycleService service = CreateService(
            store,
            CreateAssetCatalog(enrolled.CaptureId, 2),
            new TestCleanupCoordinator());
        _ = await service.ClearMemoryAsync();
        CaptureAnalysisEnrollment cleared = store.Snapshot.State.Enrollments.Single();

        CaptureAnalysisExclusionResult result = await service.ExcludeAsync(
            new CaptureAnalysisExclusionRequest(enrolled.CaptureId, kind));

        Assert.AreEqual(CaptureAnalysisExclusionStatus.Succeeded, result.Status);
        CaptureAnalysisEnrollment excluded = store.Snapshot.State.Enrollments.Single();
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Excluded, excluded.State);
        Assert.AreEqual(reason, excluded.ExclusionReason);
        Assert.AreEqual(cleared.EnrollmentGeneration + 1, excluded.EnrollmentGeneration);
        Assert.AreEqual(cleared.TombstoneGeneration + 1, excluded.TombstoneGeneration);
    }

    [TestMethod]
    public async Task ClearMemoryAsync_ShouldResetWatermarkBeforeCleanupAndKeepFutureAdmissionEnabled()
    {
        List<string> ordering = [];
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled), ordering);
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 8);
        var cleanup = new TestCleanupCoordinator(ordering);
        using CaptureAnalysisLifecycleService service = CreateService(store, assets, cleanup);

        CaptureAnalysisMaintenanceResult result = await service.ClearMemoryAsync();

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Succeeded, result.Status);
        Assert.AreEqual(1, result.AffectedCaptureCount);
        CollectionAssert.AreEqual(new[] { "control", "cleanup" }, ordering);
        Assert.IsTrue(store.Snapshot.State.IsFutureCaptureAdmissionEnabled);
        Assert.AreEqual(8, store.Snapshot.State.FutureCaptureSequenceWatermark);
        Assert.AreEqual(CaptureAnalysisBackfillState.NotAuthorized, store.Snapshot.State.BackfillState);
        Assert.AreEqual(
            CaptureAnalysisEnrollmentState.Excluded,
            store.Snapshot.State.Enrollments.Single().State);
        Assert.AreEqual(
            CaptureAnalysisExclusionReason.MemoryCleared,
            store.Snapshot.State.Enrollments.Single().ExclusionReason);
    }

    [TestMethod]
    public async Task RebuildSearchIndexAsync_ShouldOnlyInvokeMetadataProjectionMaintenance()
    {
        var projection = new Mock<ICaptureAnalysisProjectionMaintenance>(MockBehavior.Strict);
        projection.Setup(service => service.RebuildAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(7));
        using CaptureAnalysisLifecycleService service = new(
            Mock.Of<ICaptureAnalysisControlStore>(),
            Mock.Of<ICaptureAssetCatalog>(),
            new TestCleanupCoordinator(),
            projection.Object,
            Mock.Of<IUserInitiatedAnalysisCapabilityPreparationService>(),
            Mock.Of<ICaptureAnalysisScheduler>());

        CaptureAnalysisMaintenanceResult result = await service.RebuildSearchIndexAsync();

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Succeeded, result.Status);
        Assert.AreEqual(7, result.AffectedCaptureCount);
        projection.VerifyAll();
    }

    [TestMethod]
    public async Task ReanalyzeCapturesAsync_ShouldPrepareCapabilitiesAndForceDurableRequeue()
    {
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled));
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 2);
        assets.Setup(catalog => catalog.Get(enrolled.CaptureId)).Returns(
            CreateAsset(enrolled.CaptureId, CaptureSourceOwnership.AppOwned));
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>(
            MockBehavior.Strict);
        preparation.Setup(service => service.PrepareAsync(
                It.IsAny<AnalysisCapabilityPreparationRequest>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Ready(
                AnalysisTestData.CreateAnalyzer(),
                ProcessingBoundary.OnDevice));
        CaptureAnalysisScheduleRequest? scheduledRequest = null;
        var scheduler = new Mock<ICaptureAnalysisScheduler>(MockBehavior.Strict);
        scheduler.Setup(service => service.ScheduleAsync(
                It.IsAny<CaptureAnalysisScheduleRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<CaptureAnalysisScheduleRequest, CancellationToken>((request, _) =>
                scheduledRequest = request)
            .Returns(ValueTask.FromResult(new CaptureAnalysisScheduleResult(
                CaptureAnalysisScheduleStatus.Scheduled,
                durableIntentCount: 3)));
        using CaptureAnalysisLifecycleService service = new(
            store,
            assets.Object,
            new TestCleanupCoordinator(),
            Mock.Of<ICaptureAnalysisProjectionMaintenance>(),
            preparation.Object,
            scheduler.Object);

        CaptureAnalysisMaintenanceResult result = await service.ReanalyzeCapturesAsync(
            new CaptureAnalysisReanalysisRequest(
                CaptureAnalysisReanalysisScope.AllEnrolledCaptures));

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Succeeded, result.Status);
        Assert.AreEqual(1, result.AffectedCaptureCount);
        Assert.IsNotNull(scheduledRequest);
        Assert.IsTrue(scheduledRequest.ForceReanalysis);
        preparation.Verify(service => service.PrepareAsync(
            It.IsAny<AnalysisCapabilityPreparationRequest>(),
            null,
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        scheduler.VerifyAll();
    }

    [TestMethod]
    public async Task ClearThenReanalyze_ShouldRestoreOnlyClearedEnrollmentsAfterCleanup()
    {
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        CaptureId excludedId = CaptureId.New();
        var userExcluded = new CaptureAnalysisEnrollment(
            excludedId,
            CaptureAnalysisEnrollmentState.Excluded,
            CaptureAnalysisExclusionReason.UserExcluded,
            enrollmentGeneration: 2,
            tombstoneGeneration: 1,
            assetFinalizationSequence: 3,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        var store = new TestControlStore(CreateControl(enrolled, userExcluded));
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 8);
        assets.Setup(catalog => catalog.Get(enrolled.CaptureId)).Returns(
            CreateAsset(enrolled.CaptureId, CaptureSourceOwnership.AppOwned));
        var cleanup = new TestCleanupCoordinator();
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(service => service.PrepareAsync(
                It.IsAny<AnalysisCapabilityPreparationRequest>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Ready(
                AnalysisTestData.CreateAnalyzer(),
                ProcessingBoundary.OnDevice));
        CaptureAnalysisScheduleRequest? scheduledRequest = null;
        var scheduler = new Mock<ICaptureAnalysisScheduler>();
        scheduler.Setup(service => service.ScheduleAsync(
                It.IsAny<CaptureAnalysisScheduleRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<CaptureAnalysisScheduleRequest, CancellationToken>((request, _) =>
                scheduledRequest = request)
            .ReturnsAsync(new CaptureAnalysisScheduleResult(
                CaptureAnalysisScheduleStatus.Scheduled,
                durableIntentCount: 3));
        using var service = new CaptureAnalysisLifecycleService(
            store,
            assets.Object,
            cleanup,
            Mock.Of<ICaptureAnalysisProjectionMaintenance>(),
            preparation.Object,
            scheduler.Object);

        CaptureAnalysisMaintenanceResult cleared = await service.ClearMemoryAsync();
        CaptureAnalysisEnrollment tombstone = store.Snapshot.State.Enrollments.Single(value =>
            value.CaptureId == enrolled.CaptureId);
        CaptureAnalysisMaintenanceResult reanalyzed = await service.ReanalyzeCapturesAsync(
            new CaptureAnalysisReanalysisRequest(
                CaptureAnalysisReanalysisScope.AllEnrolledCaptures));

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Succeeded, cleared.Status);
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Excluded, tombstone.State);
        Assert.AreEqual(CaptureAnalysisExclusionReason.MemoryCleared, tombstone.ExclusionReason);
        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Succeeded, reanalyzed.Status);
        Assert.AreEqual(1, reanalyzed.AffectedCaptureCount);
        CaptureAnalysisEnrollment restored = store.Snapshot.State.Enrollments.Single(value =>
            value.CaptureId == enrolled.CaptureId);
        CaptureAnalysisEnrollment stillExcluded = store.Snapshot.State.Enrollments.Single(value =>
            value.CaptureId == excludedId);
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Enrolled, restored.State);
        Assert.AreEqual(CaptureAnalysisExclusionReason.None, restored.ExclusionReason);
        Assert.AreEqual(tombstone.EnrollmentGeneration + 1, restored.EnrollmentGeneration);
        Assert.AreEqual(tombstone.TombstoneGeneration, restored.TombstoneGeneration);
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Excluded, stillExcluded.State);
        Assert.AreEqual(CaptureAnalysisExclusionReason.UserExcluded, stillExcluded.ExclusionReason);
        Assert.IsNotNull(scheduledRequest);
        Assert.IsTrue(scheduledRequest.ForceReanalysis);
        Assert.AreEqual(2, cleanup.ReconcileCount);
    }

    [TestMethod]
    public async Task ReanalyzeAfterIncompleteClear_ShouldKeepTheCleanupTombstoneAndNotSchedule()
    {
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled));
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 8);
        var cleanup = new TestCleanupCoordinator { Result = false };
        var scheduler = new Mock<ICaptureAnalysisScheduler>(MockBehavior.Strict);
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(service => service.PrepareAsync(
                It.IsAny<AnalysisCapabilityPreparationRequest>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Ready(
                AnalysisTestData.CreateAnalyzer(),
                ProcessingBoundary.OnDevice));
        using var service = new CaptureAnalysisLifecycleService(
            store,
            assets.Object,
            cleanup,
            Mock.Of<ICaptureAnalysisProjectionMaintenance>(),
            preparation.Object,
            scheduler.Object);

        CaptureAnalysisMaintenanceResult cleared = await service.ClearMemoryAsync();
        CaptureAnalysisMaintenanceResult reanalyzed = await service.ReanalyzeCapturesAsync(
            new CaptureAnalysisReanalysisRequest(
                CaptureAnalysisReanalysisScope.AllEnrolledCaptures));

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Incomplete, cleared.Status);
        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Incomplete, reanalyzed.Status);
        CaptureAnalysisEnrollment tombstone = store.Snapshot.State.Enrollments.Single();
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Excluded, tombstone.State);
        Assert.AreEqual(CaptureAnalysisExclusionReason.MemoryCleared, tombstone.ExclusionReason);
        scheduler.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReanalyzeCapturesAsync_ShouldReportPreparationAndSchedulingAndAllowOptionalModelToBeUnavailable()
    {
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled));
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 2);
        assets.Setup(catalog => catalog.Get(enrolled.CaptureId)).Returns(
            CreateAsset(enrolled.CaptureId, CaptureSourceOwnership.AppOwned));
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(service => service.PrepareAsync(
                It.Is<AnalysisCapabilityPreparationRequest>(request =>
                    request.Capability.Id == AnalysisCapabilities.ImageDescriptionV1.Id),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Unsupported(new AnalysisFailure(
                AnalysisFailureCode.CapabilityUnavailable,
                AnalysisFailureDisposition.Terminal)));
        preparation.Setup(service => service.PrepareAsync(
                It.Is<AnalysisCapabilityPreparationRequest>(request =>
                    request.Capability.Id != AnalysisCapabilities.ImageDescriptionV1.Id),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .Returns<AnalysisCapabilityPreparationRequest,
                IProgress<AnalysisCapabilityPreparationProgress>?,
                CancellationToken>((_, progress, _) =>
                {
                    progress?.Report(new AnalysisCapabilityPreparationProgress(1));
                    return Task.FromResult(AnalysisCapabilityPreparationState.Ready(
                        AnalysisTestData.CreateAnalyzer(),
                        ProcessingBoundary.OnDevice));
                });
        var scheduler = new Mock<ICaptureAnalysisScheduler>();
        scheduler.Setup(service => service.ScheduleAsync(
                It.IsAny<CaptureAnalysisScheduleRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisScheduleResult(
                CaptureAnalysisScheduleStatus.Scheduled,
                durableIntentCount: 1));
        using CaptureAnalysisLifecycleService service = new(
            store,
            assets.Object,
            new TestCleanupCoordinator(),
            Mock.Of<ICaptureAnalysisProjectionMaintenance>(),
            preparation.Object,
            scheduler.Object);
        var progress = new RecordingMaintenanceProgress();

        CaptureAnalysisMaintenanceResult result = await service.ReanalyzeCapturesAsync(
            new CaptureAnalysisReanalysisRequest(
                CaptureAnalysisReanalysisScope.AllEnrolledCaptures),
            progress);

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Succeeded, result.Status);
        Assert.IsTrue(progress.Values.Any(value =>
            value.Phase == CaptureAnalysisMaintenancePhase.PreparingModels));
        Assert.IsTrue(progress.Values.Any(value =>
            value.Phase == CaptureAnalysisMaintenancePhase.SchedulingCaptures));
        Assert.AreEqual(1, progress.Values[^1].FractionComplete);
    }

    [TestMethod]
    public async Task ReanalyzeCapturesAsync_WhenRequiredPreparationFails_ShouldStillQueueAvailableCapabilities()
    {
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled));
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 2);
        assets.Setup(catalog => catalog.Get(enrolled.CaptureId)).Returns(
            CreateAsset(enrolled.CaptureId, CaptureSourceOwnership.AppOwned));
        var attempted = new List<AnalysisCapabilityId>();
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(service => service.PrepareAsync(
                It.IsAny<AnalysisCapabilityPreparationRequest>(),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(),
                It.IsAny<CancellationToken>()))
            .Returns<AnalysisCapabilityPreparationRequest,
                IProgress<AnalysisCapabilityPreparationProgress>?,
                CancellationToken>((request, _, _) =>
                {
                    attempted.Add(request.Capability.Id);
                    return Task.FromResult(
                        request.Capability.Id == AnalysisCapabilities.MediaPropertiesV1.Id
                            ? AnalysisCapabilityPreparationState.Failed(new AnalysisFailure(
                                AnalysisFailureCode.ProviderUnavailable,
                                AnalysisFailureDisposition.Transient))
                            : AnalysisCapabilityPreparationState.Ready(
                                AnalysisTestData.CreateAnalyzer(),
                                ProcessingBoundary.OnDevice));
                });
        var scheduler = new Mock<ICaptureAnalysisScheduler>(MockBehavior.Strict);
        scheduler.Setup(service => service.ScheduleAsync(
                It.IsAny<CaptureAnalysisScheduleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisScheduleResult(CaptureAnalysisScheduleStatus.Scheduled, 3));
        using CaptureAnalysisLifecycleService service = new(
            store,
            assets.Object,
            new TestCleanupCoordinator(),
            Mock.Of<ICaptureAnalysisProjectionMaintenance>(),
            preparation.Object,
            scheduler.Object);

        CaptureAnalysisMaintenanceResult result = await service.ReanalyzeCapturesAsync(
            new CaptureAnalysisReanalysisRequest(
                CaptureAnalysisReanalysisScope.AllEnrolledCaptures),
            new RecordingMaintenanceProgress());

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Incomplete, result.Status);
        Assert.AreEqual(1, result.AffectedCaptureCount);
        CollectionAssert.AreEqual(
            new[]
            {
                AnalysisCapabilities.MediaPropertiesV1.Id,
                AnalysisCapabilities.OcrDocumentV1.Id,
                AnalysisCapabilities.ImageDescriptionV1.Id,
            },
            attempted.ToArray());
        scheduler.Verify(service => service.ScheduleAsync(
            It.Is<CaptureAnalysisScheduleRequest>(request => request.ForceReanalysis),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task ReanalyzeMixedLibrary_WhenSpeechIsUnavailable_ShouldStillQueueImages(bool throws, bool allUnavailable)
    {
        CaptureAnalysisEnrollment image = CreateEnrollment(2);
        CaptureId audioId = CaptureId.New();
        CaptureAnalysisRecipe audioRecipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryAudioRecipe();
        var audio = new CaptureAnalysisEnrollment(audioId, CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None, 1, 0, 3, audioRecipe.Id, audioRecipe.Version);
        var store = new TestControlStore(CreateControl(image, audio));
        var assets = CreateAssetCatalog(image.CaptureId, 3);
        assets.Setup(catalog => catalog.Get(image.CaptureId)).Returns(
            CreateAsset(image.CaptureId, CaptureSourceOwnership.AppOwned));
        assets.Setup(catalog => catalog.Get(audioId)).Returns(new CaptureAsset(audioId,
            CaptureFileType.Audio, @"C:\CaptureTool\Captures\audio.wav",
            CaptureSourceOwnership.AppOwned, AnalysisTestData.CapturedAtUtc));
        assets.Setup(catalog => catalog.GetChangesAfter(0)).Returns([
            new CaptureAssetChange(2, image.CaptureId, 1, CaptureAssetChangeType.Finalized, AnalysisTestData.CapturedAtUtc),
            new CaptureAssetChange(3, audioId, 1, CaptureAssetChangeType.Finalized, AnalysisTestData.CapturedAtUtc),
        ]);
        var preparation = new Mock<IUserInitiatedAnalysisCapabilityPreparationService>();
        preparation.Setup(service => service.PrepareAsync(It.IsAny<AnalysisCapabilityPreparationRequest>(),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns<AnalysisCapabilityPreparationRequest, IProgress<AnalysisCapabilityPreparationProgress>?, CancellationToken>(
                (request, _, _) => allUnavailable || request.MediaKind == CaptureMediaKind.Audio
                    ? throws ? Task.FromException<AnalysisCapabilityPreparationState>(new IOException("Provider unavailable"))
                        : Task.FromResult(AnalysisCapabilityPreparationState.Unsupported(new AnalysisFailure(
                            AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal)))
                    : Task.FromResult(AnalysisCapabilityPreparationState.Ready(
                        AnalysisTestData.CreateAnalyzer(), ProcessingBoundary.OnDevice)));
        var scheduler = new Mock<ICaptureAnalysisScheduler>(MockBehavior.Strict);
        scheduler.Setup(service => service.ScheduleAsync(
                It.Is<CaptureAnalysisScheduleRequest>(request =>
                    request.Admission.CaptureId == image.CaptureId && request.ForceReanalysis),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureAnalysisScheduleResult(CaptureAnalysisScheduleStatus.Scheduled, 3));
        using var service = new CaptureAnalysisLifecycleService(store, assets.Object,
            new TestCleanupCoordinator(), Mock.Of<ICaptureAnalysisProjectionMaintenance>(), preparation.Object, scheduler.Object);

        var result = await service.ReanalyzeCapturesAsync(new(CaptureAnalysisReanalysisScope.AllEnrolledCaptures));

        Assert.AreEqual(CaptureAnalysisMaintenanceStatus.Incomplete, result.Status);
        Assert.AreEqual(allUnavailable ? 0 : 1, result.AffectedCaptureCount);
        scheduler.Verify(service => service.ScheduleAsync(It.IsAny<CaptureAnalysisScheduleRequest>(),
            It.IsAny<CancellationToken>()), allUnavailable ? Times.Never() : Times.Once());
    }

    [TestMethod]
    public async Task ForgetHistory_ShouldTombstoneBeforeRemovingDerivedState()
    {
        List<string> ordering = [];
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled), ordering);
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 2);
        var cleanup = new TestCleanupCoordinator(ordering);
        using CaptureAnalysisLifecycleService service = CreateService(store, assets, cleanup);
        var request = new CaptureAssetRemovalRequest(
            enrolled.CaptureId,
            CaptureAssetRemovalKind.ForgetHistory);

        CaptureAssetRemovalResult result = await service.RemoveAsync(request);

        Assert.AreEqual(CaptureAssetRemovalStatus.Succeeded, result.Status);
        CollectionAssert.AreEqual(new[] { "control", "cleanup" }, ordering);
        CaptureAnalysisEnrollment tombstone = store.Snapshot.State.Enrollments.Single();
        Assert.AreEqual(CaptureAnalysisEnrollmentState.Forgotten, tombstone.State);
        Assert.AreEqual(CaptureAnalysisExclusionReason.HistoryForgotten, tombstone.ExclusionReason);
    }

    [TestMethod]
    public async Task DeleteRetainedSource_ShouldFailClosedWithoutConfirmationOrOwnership()
    {
        CaptureId captureId = CaptureId.New();
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(captureId, 1);
        CaptureAsset external = CreateAsset(captureId, CaptureSourceOwnership.LegacyExternal);
        assets.Setup(catalog => catalog.Get(captureId)).Returns(external);
        var cleanup = new TestCleanupCoordinator();
        using CaptureAnalysisLifecycleService service = CreateService(
            new TestControlStore(CreateControl()),
            assets,
            cleanup);

        CaptureAssetRemovalResult unconfirmed = await service.RemoveAsync(
            new CaptureAssetRemovalRequest(
                captureId,
                CaptureAssetRemovalKind.DeleteRetainedSource));
        CaptureAssetRemovalResult externalResult = await service.RemoveAsync(
            new CaptureAssetRemovalRequest(
                captureId,
                CaptureAssetRemovalKind.DeleteRetainedSource,
                isConfirmed: true));

        Assert.AreEqual(CaptureAssetRemovalStatus.ConfirmationRequired, unconfirmed.Status);
        Assert.AreEqual(CaptureAssetRemovalStatus.OwnershipDenied, externalResult.Status);
        assets.Verify(catalog => catalog.Get(captureId), Times.Once);
    }

    [TestMethod]
    public async Task DeleteRetainedSource_ShouldPersistIntentThenRetryOnlyTheAppOwnedRetainedPath()
    {
        List<string> ordering = [];
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled), ordering);
        string retainedPath = @"C:\CaptureTool\Captures\retained.png";
        string preferredExportPath = @"D:\Pictures\export.png";
        var asset = new CaptureAsset(
            enrolled.CaptureId,
            CaptureFileType.Image,
            retainedPath,
            CaptureSourceOwnership.AppOwned,
            preferredExportPath,
            AnalysisTestData.CapturedAtUtc,
            CaptureAssetLifecycleState.Active,
            lifecycleRevision: 1);
        var assets = new Mock<ICaptureAssetCatalog>(MockBehavior.Strict);
        assets.Setup(catalog => catalog.Get(enrolled.CaptureId)).Returns(asset);
        assets.Setup(catalog => catalog.GetChangesAfter(0)).Returns(
        [
            new CaptureAssetChange(
                2,
                enrolled.CaptureId,
                lifecycleRevision: 1,
                CaptureAssetChangeType.Finalized,
                AnalysisTestData.CapturedAtUtc),
        ]);

        var failingFiles = new Mock<IFileSystem>(MockBehavior.Strict);
        failingFiles.Setup(fileSystem => fileSystem.FileExists(retainedPath)).Returns(true);
        failingFiles.Setup(fileSystem => fileSystem.DeleteFile(retainedPath))
            .Callback(() => ordering.Add("source-delete-failed"))
            .Throws(new IOException("source is temporarily locked"));
        CaptureAnalysisCleanupCoordinator firstCleanup = CreateDeletionCleanup(
            store,
            assets,
            failingFiles.Object,
            ordering,
            configureSuccessfulCleanup: false);
        using (CaptureAnalysisLifecycleService first = CreateService(
            store,
            assets,
            firstCleanup))
        {
            CaptureAssetRemovalResult incomplete = await first.RemoveAsync(
                new CaptureAssetRemovalRequest(
                    enrolled.CaptureId,
                    CaptureAssetRemovalKind.DeleteRetainedSource,
                    isConfirmed: true));

            Assert.AreEqual(CaptureAssetRemovalStatus.Incomplete, incomplete.Status);
            CaptureAnalysisEnrollment tombstone = store.Snapshot.State.Enrollments.Single();
            Assert.AreEqual(CaptureAnalysisEnrollmentState.Forgotten, tombstone.State);
            Assert.AreEqual(CaptureAnalysisExclusionReason.DeleteRequested, tombstone.ExclusionReason);
            CollectionAssert.AreEqual(
                new[] { "control", "projection", "source-delete-failed" },
                ordering);
        }

        bool retainedSourceExists = true;
        var succeedingFiles = new Mock<IFileSystem>(MockBehavior.Strict);
        succeedingFiles.Setup(fileSystem => fileSystem.FileExists(retainedPath))
            .Returns(() => retainedSourceExists);
        succeedingFiles.Setup(fileSystem => fileSystem.DeleteFile(retainedPath))
            .Callback(() =>
            {
                ordering.Add("source-delete");
                retainedSourceExists = false;
            });
        CaptureAnalysisCleanupCoordinator restartedCleanup = CreateDeletionCleanup(
            store,
            assets,
            succeedingFiles.Object,
            ordering,
            configureSuccessfulCleanup: true);
        long tombstoneRevision = store.Snapshot.DocumentRevision;
        using CaptureAnalysisLifecycleService restarted = CreateService(
            store,
            assets,
            restartedCleanup);

        CaptureAssetRemovalResult retried = await restarted.RemoveAsync(
            new CaptureAssetRemovalRequest(
                enrolled.CaptureId,
                CaptureAssetRemovalKind.DeleteRetainedSource,
                isConfirmed: true));

        Assert.AreEqual(CaptureAssetRemovalStatus.AlreadyRemoved, retried.Status);
        Assert.IsFalse(retainedSourceExists);
        Assert.AreEqual(tombstoneRevision, store.Snapshot.DocumentRevision);
        Assert.IsLessThan(ordering.IndexOf("recent"), ordering.IndexOf("source-delete"));
        Assert.IsLessThan(ordering.IndexOf("catalog"), ordering.IndexOf("source-delete"));
        failingFiles.Verify(fileSystem => fileSystem.FileExists(preferredExportPath), Times.Never);
        failingFiles.Verify(fileSystem => fileSystem.DeleteFile(preferredExportPath), Times.Never);
        succeedingFiles.Verify(fileSystem => fileSystem.FileExists(preferredExportPath), Times.Never);
        succeedingFiles.Verify(fileSystem => fileSystem.DeleteFile(preferredExportPath), Times.Never);
    }

    [TestMethod]
    public async Task DeleteRetainedSource_ShouldRecheckOwnershipAfterTheDurableIntent()
    {
        CaptureAnalysisEnrollment tombstone = new(
            AnalysisTestData.CaptureId,
            CaptureAnalysisEnrollmentState.Forgotten,
            CaptureAnalysisExclusionReason.DeleteRequested,
            enrollmentGeneration: 2,
            tombstoneGeneration: 1,
            assetFinalizationSequence: 2,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        var store = new TestControlStore(CreateControl(tombstone));
        CaptureAsset external = CreateAsset(
            tombstone.CaptureId,
            CaptureSourceOwnership.LegacyExternal);
        var assets = new Mock<ICaptureAssetCatalog>(MockBehavior.Strict);
        assets.Setup(catalog => catalog.Get(tombstone.CaptureId)).Returns(external);
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        var projection = new Mock<ICaptureAnalysisProjectionMaintenance>(MockBehavior.Strict);
        projection.Setup(index => index.RemoveAsync(
                tombstone.CaptureId,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var coordinator = new CaptureAnalysisCleanupCoordinator(
            store,
            Mock.Of<ICaptureAnalysisJobStore>(MockBehavior.Strict),
            Mock.Of<ICaptureAnalysisCheckpointStore>(),
            Mock.Of<ICaptureAnalysisStore>(MockBehavior.Strict),
            Mock.Of<ICaptureAnalysisMutationCoordinator>(MockBehavior.Strict),
            projection.Object,
            assets.Object,
            Mock.Of<IRecentCaptureCatalog>(MockBehavior.Strict),
            fileSystem.Object);

        bool result = await coordinator.ReconcileCaptureAsync(tombstone.CaptureId);

        Assert.IsFalse(result);
        projection.VerifyAll();
        assets.VerifyAll();
        fileSystem.Verify(file => file.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task CleanupCoordinator_ShouldForgetPathsOnlyAfterDerivedCleanup()
    {
        CaptureAnalysisEnrollment tombstone = new(
            AnalysisTestData.CaptureId,
            CaptureAnalysisEnrollmentState.Forgotten,
            CaptureAnalysisExclusionReason.HistoryForgotten,
            enrollmentGeneration: 2,
            tombstoneGeneration: 1,
            assetFinalizationSequence: 2,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        var store = new TestControlStore(CreateControl(tombstone));
        var jobs = new Mock<ICaptureAnalysisJobStore>(MockBehavior.Strict);
        jobs.Setup(jobStore => jobStore.CancelCaptureAsync(
                tombstone.CaptureId,
                tombstone.TombstoneGeneration,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(1));
        var metadata = new Mock<ICaptureAnalysisStore>(MockBehavior.Strict);
        metadata.Setup(metadataStore => metadataStore.GetAsync(
                tombstone.CaptureId,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<CaptureAnalysisStoreSnapshot?>(null));
        var projection = new Mock<ICaptureAnalysisProjectionMaintenance>(MockBehavior.Strict);
        projection.Setup(index => index.RemoveAsync(
                tombstone.CaptureId,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        CaptureAsset asset = CreateAsset(tombstone.CaptureId, CaptureSourceOwnership.AppOwned);
        var assets = new Mock<ICaptureAssetCatalog>(MockBehavior.Strict);
        assets.Setup(catalog => catalog.Get(tombstone.CaptureId)).Returns(asset);
        assets.Setup(catalog => catalog.TryForget(asset.Id, asset.LifecycleRevision))
            .Returns(new CaptureAssetCatalogWriteResult(true, true, null, 3));
        var recent = new Mock<IRecentCaptureCatalog>(MockBehavior.Strict);
        recent.Setup(catalog => catalog.GetEntries()).Returns(
        [
            new RecentCaptureCatalogEntry(
                asset.RetainedSourcePath,
                asset.MediaType,
                RecentCaptureOrigin.Captured,
                DateTime.UtcNow,
                asset.Id),
        ]);
        recent.Setup(catalog => catalog.RemoveRange(It.Is<IEnumerable<string>>(
                paths => paths.Single() == asset.RetainedSourcePath)))
            .Returns(1);
        var coordinator = new CaptureAnalysisCleanupCoordinator(
            store,
            jobs.Object,
            Mock.Of<ICaptureAnalysisCheckpointStore>(),
            metadata.Object,
            Mock.Of<ICaptureAnalysisMutationCoordinator>(),
            projection.Object,
            assets.Object,
            recent.Object,
            Mock.Of<IFileSystem>());

        bool result = await coordinator.ReconcileAsync();

        Assert.IsTrue(result);
        jobs.Verify(jobStore => jobStore.CancelCaptureAsync(
            tombstone.CaptureId,
            tombstone.TombstoneGeneration,
            It.IsAny<CancellationToken>()), Times.Once);
        metadata.VerifyAll();
        projection.VerifyAll();
        assets.VerifyAll();
        recent.VerifyAll();
    }

    [TestMethod]
    public async Task CleanupCoordinator_ShouldDeleteExcludedMetadataWithTheDurableTombstoneToken()
    {
        List<string> ordering = [];
        CaptureAnalysisEnrollment tombstone = new(
            AnalysisTestData.CaptureId,
            CaptureAnalysisEnrollmentState.Excluded,
            CaptureAnalysisExclusionReason.UserExcluded,
            enrollmentGeneration: 2,
            tombstoneGeneration: 3,
            assetFinalizationSequence: 2,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        var store = new TestControlStore(CreateControl(tombstone));
        var jobs = new Mock<ICaptureAnalysisJobStore>(MockBehavior.Strict);
        jobs.Setup(jobStore => jobStore.CancelCaptureAsync(
                tombstone.CaptureId,
                tombstone.TombstoneGeneration,
                It.IsAny<CancellationToken>()))
            .Callback(() => ordering.Add("jobs"))
            .Returns(ValueTask.FromResult(1));
        var checkpoints = new Mock<ICaptureAnalysisCheckpointStore>(MockBehavior.Strict);
        checkpoints.Setup(store => store.DeleteCaptureAsync(
                tombstone.CaptureId,
                It.IsAny<CancellationToken>()))
            .Callback(() => ordering.Add("checkpoints"))
            .Returns(ValueTask.CompletedTask);
        var metadataSnapshot = new CaptureAnalysisStoreSnapshot(
            4,
            AnalysisTestData.CreateRecord());
        var metadata = new Mock<ICaptureAnalysisStore>(MockBehavior.Strict);
        metadata.Setup(metadataStore => metadataStore.GetAsync(
                tombstone.CaptureId,
                It.IsAny<CancellationToken>()))
            .Callback(() => ordering.Add("metadata-read"))
            .Returns(ValueTask.FromResult<CaptureAnalysisStoreSnapshot?>(metadataSnapshot));
        CaptureAnalysisDeletionToken? deletionToken = null;
        var mutation = new Mock<ICaptureAnalysisMutationCoordinator>(MockBehavior.Strict);
        mutation.Setup(coordinator => coordinator.TryDeleteAsync(
                It.IsAny<CaptureAnalysisDeletionToken>(),
                metadataSnapshot.DocumentRevision,
                It.IsAny<CancellationToken>()))
            .Callback<CaptureAnalysisDeletionToken, long, CancellationToken>((token, _, _) =>
            {
                deletionToken = token;
                ordering.Add("metadata-delete");
            })
            .Returns(ValueTask.FromResult(new CaptureAnalysisStoreWriteResult(
                CaptureAnalysisStoreWriteStatus.Succeeded,
                metadataSnapshot)));
        var projection = new Mock<ICaptureAnalysisProjectionMaintenance>(MockBehavior.Strict);
        projection.Setup(index => index.RemoveAsync(
                tombstone.CaptureId,
                It.IsAny<CancellationToken>()))
            .Callback(() => ordering.Add("projection"))
            .Returns(ValueTask.CompletedTask);
        var coordinator = new CaptureAnalysisCleanupCoordinator(
            store,
            jobs.Object,
            checkpoints.Object,
            metadata.Object,
            mutation.Object,
            projection.Object,
            Mock.Of<ICaptureAssetCatalog>(MockBehavior.Strict),
            Mock.Of<IRecentCaptureCatalog>(MockBehavior.Strict),
            Mock.Of<IFileSystem>(MockBehavior.Strict));

        bool result = await coordinator.ReconcileCaptureAsync(tombstone.CaptureId);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(
            new[] { "projection", "jobs", "checkpoints", "metadata-read", "metadata-delete" },
            ordering);
        Assert.IsNotNull(deletionToken);
        Assert.AreEqual(tombstone.CaptureId, deletionToken.Value.CaptureId);
        Assert.AreEqual(tombstone.TombstoneGeneration, deletionToken.Value.TombstoneGeneration);
        Assert.AreEqual(store.Snapshot.State.ControlGeneration, deletionToken.Value.ControlGeneration);
        jobs.VerifyAll();
        checkpoints.VerifyAll();
        metadata.VerifyAll();
        mutation.VerifyAll();
        projection.VerifyAll();
    }

    [TestMethod]
    public async Task QueuedCleanup_ShouldNotDeleteAConcurrentlyRestoredEnrollment()
    {
        CaptureAnalysisEnrollment tombstone = new(
            AnalysisTestData.CaptureId,
            CaptureAnalysisEnrollmentState.Excluded,
            CaptureAnalysisExclusionReason.MemoryCleared,
            enrollmentGeneration: 2,
            tombstoneGeneration: 1,
            assetFinalizationSequence: 2,
            requestedRecipeId: null,
            requestedRecipeVersion: null);
        var store = new TestControlStore(CreateControl(tombstone));
        var gate = new CaptureAnalysisEnrollmentGate();
        var projection = new Mock<ICaptureAnalysisProjectionMaintenance>(MockBehavior.Strict);
        var coordinator = new CaptureAnalysisCleanupCoordinator(
            store,
            Mock.Of<ICaptureAnalysisJobStore>(MockBehavior.Strict),
            Mock.Of<ICaptureAnalysisCheckpointStore>(MockBehavior.Strict),
            Mock.Of<ICaptureAnalysisStore>(MockBehavior.Strict),
            Mock.Of<ICaptureAnalysisMutationCoordinator>(MockBehavior.Strict),
            projection.Object,
            Mock.Of<ICaptureAssetCatalog>(MockBehavior.Strict),
            Mock.Of<IRecentCaptureCatalog>(MockBehavior.Strict),
            Mock.Of<IFileSystem>(MockBehavior.Strict),
            gate);
        using IDisposable lease = await gate.EnterAsync(CancellationToken.None);
        Task<bool> cleanup = coordinator.ReconcileCaptureAsync(tombstone.CaptureId).AsTask();
        await Task.Yield();
        CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe();
        var restored = new CaptureAnalysisEnrollment(tombstone.CaptureId,
            CaptureAnalysisEnrollmentState.Enrolled, CaptureAnalysisExclusionReason.None,
            3, tombstone.TombstoneGeneration, tombstone.AssetFinalizationSequence, recipe.Id, recipe.Version);
        await store.TryWriteAsync(new(store.Snapshot.State.Policy, [restored]), store.Snapshot.DocumentRevision);
        lease.Dispose();

        Assert.IsTrue(await cleanup.WaitAsync(TimeSpan.FromSeconds(1)));
        projection.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ForgetHistory_ShouldResumeCleanupWithoutWritingAnotherTombstone()
    {
        CaptureAnalysisEnrollment enrolled = CreateEnrollment(2);
        var store = new TestControlStore(CreateControl(enrolled));
        Mock<ICaptureAssetCatalog> assets = CreateAssetCatalog(enrolled.CaptureId, 2);
        var firstCleanup = new TestCleanupCoordinator { Result = false };
        using (CaptureAnalysisLifecycleService first = CreateService(store, assets, firstCleanup))
        {
            CaptureAssetRemovalResult incomplete = await first.RemoveAsync(
                new CaptureAssetRemovalRequest(
                    enrolled.CaptureId,
                    CaptureAssetRemovalKind.ForgetHistory));
            Assert.AreEqual(CaptureAssetRemovalStatus.Incomplete, incomplete.Status);
        }

        long revisionAfterTombstone = store.Snapshot.DocumentRevision;
        using CaptureAnalysisLifecycleService restarted = CreateService(
            store,
            assets,
            new TestCleanupCoordinator());
        CaptureAssetRemovalResult retried = await restarted.RemoveAsync(
            new CaptureAssetRemovalRequest(
                enrolled.CaptureId,
                CaptureAssetRemovalKind.ForgetHistory));

        Assert.AreEqual(CaptureAssetRemovalStatus.AlreadyRemoved, retried.Status);
        Assert.AreEqual(revisionAfterTombstone, store.Snapshot.DocumentRevision);
    }

    private static CaptureAnalysisLifecycleService CreateService(
        ICaptureAnalysisControlStore store,
        Mock<ICaptureAssetCatalog> assets,
        ICaptureAnalysisCleanupCoordinator cleanup)
    {
        return new(
            store,
            assets.Object,
            cleanup,
            Mock.Of<ICaptureAnalysisProjectionMaintenance>(),
            Mock.Of<IUserInitiatedAnalysisCapabilityPreparationService>(),
            Mock.Of<ICaptureAnalysisScheduler>());
    }

    private static CaptureAnalysisCleanupCoordinator CreateDeletionCleanup(
        ICaptureAnalysisControlStore store,
        Mock<ICaptureAssetCatalog> assets,
        IFileSystem fileSystem,
        List<string> ordering,
        bool configureSuccessfulCleanup)
    {
        var jobs = new Mock<ICaptureAnalysisJobStore>(MockBehavior.Strict);
        var metadata = new Mock<ICaptureAnalysisStore>(MockBehavior.Strict);
        var projection = new Mock<ICaptureAnalysisProjectionMaintenance>(MockBehavior.Strict);
        var recent = new Mock<IRecentCaptureCatalog>(MockBehavior.Strict);
        projection.Setup(index => index.RemoveAsync(
                AnalysisTestData.CaptureId,
                It.IsAny<CancellationToken>()))
            .Callback(() => ordering.Add("projection"))
            .Returns(ValueTask.CompletedTask);
        if (configureSuccessfulCleanup)
        {
            jobs.Setup(jobStore => jobStore.CancelCaptureAsync(
                    AnalysisTestData.CaptureId,
                    1,
                    It.IsAny<CancellationToken>()))
                .Returns(ValueTask.FromResult(1));
            metadata.Setup(metadataStore => metadataStore.GetAsync(
                    AnalysisTestData.CaptureId,
                    It.IsAny<CancellationToken>()))
                .Returns(ValueTask.FromResult<CaptureAnalysisStoreSnapshot?>(null));
            recent.Setup(catalog => catalog.GetEntries()).Returns(
            [
                new RecentCaptureCatalogEntry(
                    @"D:\Pictures\export.png",
                    CaptureFileType.Image,
                    RecentCaptureOrigin.Captured,
                    DateTime.UtcNow,
                    AnalysisTestData.CaptureId),
            ]);
            recent.Setup(catalog => catalog.RemoveRange(It.IsAny<IEnumerable<string>>()))
                .Callback(() => ordering.Add("recent"))
                .Returns(1);
            assets.Setup(catalog => catalog.TryForget(AnalysisTestData.CaptureId, 1))
                .Callback(() => ordering.Add("catalog"))
                .Returns(new CaptureAssetCatalogWriteResult(true, true, null, 3));
        }

        return new(
            store,
            jobs.Object,
            Mock.Of<ICaptureAnalysisCheckpointStore>(),
            metadata.Object,
            Mock.Of<ICaptureAnalysisMutationCoordinator>(MockBehavior.Strict),
            projection.Object,
            assets.Object,
            recent.Object,
            fileSystem);
    }

    private static Mock<ICaptureAssetCatalog> CreateAssetCatalog(
        CaptureId captureId,
        long latestSequence)
    {
        var assets = new Mock<ICaptureAssetCatalog>();
        assets.Setup(catalog => catalog.GetLatestChangeSequence()).Returns(latestSequence);
        assets.Setup(catalog => catalog.GetChangesAfter(0)).Returns(
        [
            new CaptureAssetChange(
                2,
                captureId,
                lifecycleRevision: 1,
                CaptureAssetChangeType.Finalized,
                AnalysisTestData.CapturedAtUtc),
        ]);
        return assets;
    }

    private static CaptureAnalysisControlSnapshot CreateControl(
        params CaptureAnalysisEnrollment[] enrollments)
    {
        CaptureAnalysisPolicy policy = CaptureAnalysisPolicy.Unknown.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
            currentSequence: 1);
        return new(1, new CaptureAnalysisControlState(policy, enrollments));
    }

    private static CaptureAnalysisEnrollment CreateEnrollment(long finalizationSequence)
    {
        CaptureAnalysisRecipe recipe = CaptureAnalysisRecipeDefaults.CreateCaptureMemoryImageRecipe();
        return new(
            AnalysisTestData.CaptureId,
            CaptureAnalysisEnrollmentState.Enrolled,
            CaptureAnalysisExclusionReason.None,
            enrollmentGeneration: 1,
            tombstoneGeneration: 0,
            finalizationSequence,
            recipe.Id,
            recipe.Version);
    }

    private static CaptureAsset CreateAsset(
        CaptureId captureId,
        CaptureSourceOwnership ownership)
    {
        return new(
            captureId,
            CaptureFileType.Image,
            @"C:\CaptureTool\Captures\capture.png",
            ownership,
            AnalysisTestData.CapturedAtUtc);
    }

    private sealed class TestControlStore(
        CaptureAnalysisControlSnapshot snapshot,
        List<string>? ordering = null) : ICaptureAnalysisControlStore
    {
        public CaptureAnalysisControlSnapshot Snapshot { get; private set; } = snapshot;

        public ValueTask<CaptureAnalysisControlSnapshot> GetAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);

        public ValueTask<CaptureAnalysisControlWriteResult> TryWriteAsync(
            CaptureAnalysisControlState state,
            long expectedDocumentRevision,
            CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(expectedDocumentRevision, Snapshot.DocumentRevision);
            ordering?.Add("control");
            Snapshot = new(Snapshot.DocumentRevision + 1, state);
            return ValueTask.FromResult(new CaptureAnalysisControlWriteResult(
                CaptureAnalysisControlWriteStatus.Succeeded,
                Snapshot));
        }
    }

    private sealed class TestCleanupCoordinator(List<string>? ordering = null) :
        ICaptureAnalysisCleanupCoordinator
    {
        public bool Result { get; set; } = true;

        public int ReconcileCount { get; private set; }

        public ValueTask<bool> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            ReconcileCount++;
            ordering?.Add("cleanup");
            return ValueTask.FromResult(Result);
        }

        public ValueTask<bool> ReconcileCaptureAsync(
            CaptureId captureId,
            CancellationToken cancellationToken = default)
        {
            ordering?.Add("cleanup");
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class RecordingMaintenanceProgress :
        IProgress<CaptureAnalysisMaintenanceProgress>
    {
        public List<CaptureAnalysisMaintenanceProgress> Values { get; } = [];

        public void Report(CaptureAnalysisMaintenanceProgress value) => Values.Add(value);
    }
}
