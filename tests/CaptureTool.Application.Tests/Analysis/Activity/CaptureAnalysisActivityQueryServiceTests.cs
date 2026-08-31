using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Analysis.Activity;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Activity;

[TestClass]
public sealed class CaptureAnalysisActivityQueryServiceTests
{
    [TestMethod]
    public async Task GetCurrent_ShouldAggregateDistinctCapturesAndModelPreparation()
    {
        CaptureId runningCapture = new(new Guid("50dbdfbd-8a10-4b8e-b5b1-cce13c1c9e30"));
        CaptureId queuedCapture = new(new Guid("bf6e76de-94c9-4a1b-bf9c-fb93c2a5f8d0"));
        CaptureId waitingCapture = new(new Guid("ba3be6a7-ff0a-4368-9ed0-55379bf25d17"));
        CaptureAnalysisJobIntent[] jobs =
        [
            CreateJob(runningCapture, AnalysisCapabilities.OcrDocumentV1, CaptureAnalysisJobState.Pending),
            CreateJob(runningCapture, AnalysisCapabilities.ImageDescriptionV1, CaptureAnalysisJobState.Running),
            CreateJob(queuedCapture, AnalysisCapabilities.SpeechTranscriptV1, CaptureAnalysisJobState.Pending),
            CreateJob(waitingCapture, AnalysisCapabilities.VideoOcrTrackV1, CaptureAnalysisJobState.WaitingForCapability),
        ];
        var jobStore = new Mock<ICaptureAnalysisJobStore>(MockBehavior.Strict);
        jobStore
            .Setup(store => store.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(jobs));
        var policyService = new Mock<ICaptureMemoryWorkflow>(MockBehavior.Strict);
        policyService
            .Setup(service => service.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryWorkflowSnapshot(new CaptureAnalysisPolicySnapshot(
                CaptureAnalysisPolicySnapshotStatus.FeatureDisabled,
                CaptureAnalysisConsentState.Unknown), null));
        var preparationQuery = new Mock<IAnalysisCapabilityPreparationActivityQueryService>(
            MockBehavior.Strict);
        preparationQuery
            .Setup(query => query.GetCurrentPreparations())
            .Returns(
            [
                new CaptureAnalysisModelPreparationActivity(
                    AnalysisTestData.CreateAnalyzer(),
                    AnalysisCapabilities.SpeechTranscriptV1,
                    CaptureMediaKind.Audio,
                    0.35),
            ]);
        var service = new CaptureAnalysisActivityQueryService(
            jobStore.Object,
            policyService.Object,
            preparationQuery.Object);

        CaptureAnalysisActivitySnapshot snapshot = await service.GetCurrentAsync();

        Assert.HasCount(1, snapshot.ModelPreparations);
        Assert.AreEqual(1, snapshot.RunningCaptureCount);
        Assert.AreEqual(1, snapshot.QueuedCaptureCount);
        Assert.AreEqual(1, snapshot.WaitingCaptureCount);
        Assert.AreEqual(0, snapshot.RetryCaptureCount);
        Assert.AreEqual(0, snapshot.FailedCaptureCount);
        Assert.IsFalse(snapshot.IsBackfillInProgress);
        Assert.IsTrue(snapshot.HasActivity);
    }

    [TestMethod]
    public async Task GetCurrent_WhenDurableJobsAreUnavailable_ShouldStillExposeModelPreparation()
    {
        var jobStore = new Mock<ICaptureAnalysisJobStore>(MockBehavior.Strict);
        jobStore
            .Setup(store => store.ReadAllAsync(It.IsAny<CancellationToken>()))
            .Throws(new IOException("Job diagnostics unavailable."));
        var policyService = new Mock<ICaptureMemoryWorkflow>(MockBehavior.Strict);
        policyService
            .Setup(service => service.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryWorkflowSnapshot(new CaptureAnalysisPolicySnapshot(
                CaptureAnalysisPolicySnapshotStatus.FeatureDisabled,
                CaptureAnalysisConsentState.Unknown), null));
        var preparationQuery = new Mock<IAnalysisCapabilityPreparationActivityQueryService>(
            MockBehavior.Strict);
        preparationQuery
            .Setup(query => query.GetCurrentPreparations())
            .Returns(
            [
                new CaptureAnalysisModelPreparationActivity(
                    AnalysisTestData.CreateAnalyzer(),
                    AnalysisCapabilities.SpeechTranscriptV1,
                    CaptureMediaKind.Audio,
                    0.6),
            ]);
        var service = new CaptureAnalysisActivityQueryService(
            jobStore.Object,
            policyService.Object,
            preparationQuery.Object);

        CaptureAnalysisActivitySnapshot snapshot = await service.GetCurrentAsync();

        Assert.HasCount(1, snapshot.ModelPreparations);
        Assert.AreEqual(0.6, snapshot.ModelPreparations[0].FractionComplete);
        Assert.IsTrue(snapshot.HasActivity);
    }

    private static CaptureAnalysisJobIntent CreateJob(
        CaptureId captureId,
        CapabilityDefinition capability,
        CaptureAnalysisJobState state)
    {
        var key = new CaptureAnalysisJobKey(
            AnalysisTestData.CreatePreconditions(captureId: captureId),
            capability,
            ProcessingBoundary.OnDevice);
        return new CaptureAnalysisJobIntent(
            key,
            state,
            attemptCount: 0,
            AnalysisTestData.GeneratedAtUtc,
            nextAttemptAtUtc: null,
            latestFailure: null,
            attempts: []);
    }

    private static async IAsyncEnumerable<CaptureAnalysisJobIntent> ToAsyncEnumerable(
        IEnumerable<CaptureAnalysisJobIntent> jobs)
    {
        foreach (CaptureAnalysisJobIntent job in jobs)
        {
            yield return job;
        }

        await Task.CompletedTask;
    }
}
