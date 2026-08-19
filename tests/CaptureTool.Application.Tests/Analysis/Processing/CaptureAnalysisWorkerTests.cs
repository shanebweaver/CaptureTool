using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Checkpoints;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Orchestration;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Processing;
using CaptureTool.Application.Abstractions.Analysis.Sources;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Analysis.Intake;
using CaptureTool.Application.Analysis.Processing;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Processing;

[TestClass]
public sealed class CaptureAnalysisWorkerTests
{
    [TestMethod]
    public async Task Run_ShouldCommitSuccessfulResultCompleteJobAndRefreshProjection()
    {
        using var fixture = new WorkerFixture();

        await fixture.RunAsync();

        fixture.Mutation.Verify(coordinator => coordinator.TryCommitCapabilityAsync(
            It.Is<AnalysisCommitToken>(token =>
                token.CaptureId == fixture.Intent.Key.CaptureId &&
                token.Capability == fixture.Intent.Key.Capability &&
                token.AnalyzerRevision == fixture.AnalyzerIdentity.Revision),
            It.Is<CanonicalCapabilityResult>(result =>
                result.CaptureId == fixture.Intent.Key.CaptureId &&
                result.Payload.Definition == fixture.Intent.Key.Capability),
            fixture.Snapshot!.DocumentRevision,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.JobStore.Verify(store => store.TryCompleteAsync(
            fixture.Lease.LeaseToken,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Projection.Verify(projection => projection.RefreshAsync(
            fixture.Intent.Key.CaptureId,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Source.Verify(source => source.DisposeAsync(), Times.Once);
        Assert.IsTrue(fixture.ResolutionRequests.Single()
            .AllowReadyFallbackWhenPreparationRequired);
        fixture.VerifyCheckpointCleared();
    }

    [TestMethod]
    public async Task Run_ShouldPassNormalizedDependenciesAndCommitExactInputProvenance()
    {
        using var fixture = new WorkerFixture();
        fixture.ConfigureDependencyInput();

        await fixture.RunAsync();

        fixture.Analyzer.Verify(analyzer => analyzer.AnalyzeAsync(
            It.Is<CaptureAnalysisRequest>(request =>
                request.Inputs.Count == 1 &&
                request.Inputs[0].ResultId == fixture.DependencyResult!.ResultId),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Mutation.Verify(coordinator => coordinator.TryCommitCapabilityAsync(
            It.IsAny<AnalysisCommitToken>(),
            It.Is<CanonicalCapabilityResult>(result =>
                result.Inputs.Count == 1 &&
                result.Inputs[0] == fixture.DependencyResult!.Reference),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.JobStore.Verify(store => store.ResumeWaitingForDependencyAsync(
            fixture.Intent.Key.CaptureId,
            fixture.Intent.Key.Capability,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldWaitForMissingDependencyWithoutSelectingOrInvokingAnalyzer()
    {
        using var fixture = new WorkerFixture();
        fixture.ConfigureMissingDependency();

        await fixture.RunAsync();

        fixture.JobStore.Verify(store => store.TryWaitForCapabilityAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure?>(failure =>
                failure.HasValue &&
                failure.Value.Code == AnalysisFailureCode.CapabilityUnavailable),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Resolver.Verify(resolver => resolver.ResolveAsync(
            It.IsAny<CaptureAnalyzerResolutionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Analyzer.Verify(analyzer => analyzer.AnalyzeAsync(
            It.IsAny<CaptureAnalysisRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Run_ShouldWaitWhenAnalyzerRequiresPreparation()
    {
        using var fixture = new WorkerFixture();
        fixture.Resolutions = [fixture.WaitingForPreparationResolution];

        await fixture.RunAsync();

        fixture.JobStore.Verify(store => store.TryWaitForCapabilityAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure?>(failure =>
                failure.HasValue && failure.Value.Code == AnalysisFailureCode.ModelNotReady),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Analyzer.Verify(analyzer => analyzer.AnalyzeAsync(
            It.IsAny<CaptureAnalysisRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Run_ShouldWaitWhenNoAnalyzerIsEligible()
    {
        using var fixture = new WorkerFixture();
        fixture.Resolutions = [CaptureAnalyzerResolution.NoEligibleAnalyzer([])];

        await fixture.RunAsync();

        fixture.JobStore.Verify(store => store.TryWaitForCapabilityAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure?>(failure =>
                failure.HasValue && failure.Value.Code == AnalysisFailureCode.CapabilityUnavailable),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldCancelWhenResolutionPolicyRevisionIsStale()
    {
        using var fixture = new WorkerFixture
        {
            ResolutionPolicyRevision = 3,
        };

        await fixture.RunAsync();

        fixture.VerifyCancelled();
        fixture.Metadata.Verify(store => store.GetAsync(
            It.IsAny<CaptureId>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyCaptureCheckpointsDeleted();
    }

    [TestMethod]
    public async Task Run_ShouldCancelWhenRegisteredSourceDoesNotMatchIntent()
    {
        using var fixture = new WorkerFixture
        {
            Snapshot = null,
        };

        await fixture.RunAsync();

        fixture.VerifyCancelled();
        fixture.Resolver.Verify(resolver => resolver.ResolveAsync(
            It.IsAny<CaptureAnalyzerResolutionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyCaptureCheckpointsDeleted();
    }

    [TestMethod]
    public async Task Run_ShouldCancelWhenAvailabilityAuthorizationIsDenied()
    {
        using var fixture = new WorkerFixture();
        fixture.DeniedStages.Add(CaptureAnalysisAuthorizationStage.AnalyzerAvailability);

        await fixture.RunAsync();

        fixture.VerifyCancelled();
        fixture.Resolver.Verify(resolver => resolver.ResolveAsync(
            It.IsAny<CaptureAnalyzerResolutionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyCaptureCheckpointsDeleted();
    }

    [TestMethod]
    public async Task Run_ShouldSkipDeniedAnalyzerAndWaitForAnotherCandidate()
    {
        using var fixture = new WorkerFixture();
        fixture.DeniedStages.Add(CaptureAnalysisAuthorizationStage.AnalyzerInvocation);

        await fixture.RunAsync();

        fixture.JobStore.Verify(store => store.TryWaitForCapabilityAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure?>(failure =>
                failure.HasValue && failure.Value.Code == AnalysisFailureCode.CapabilityUnavailable),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.HasCount(2, fixture.ResolutionRequests);
        Assert.IsTrue(fixture.ResolutionRequests[1].AttemptedAnalyzers.Contains(
            fixture.AnalyzerIdentity.Revision));
    }

    [TestMethod]
    public async Task Run_ShouldCancelWhenSourceAuthorizationIsDenied()
    {
        using var fixture = new WorkerFixture();
        fixture.DeniedStages.Add(CaptureAnalysisAuthorizationStage.SourceVerification);

        await fixture.RunAsync();

        fixture.VerifyCancelled();
        fixture.SourceVerifier.Verify(verifier => verifier.TryOpenVerifiedAsync(
            It.IsAny<CaptureAnalysisSourceVerificationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyCheckpointCleared();
    }

    [TestMethod]
    public async Task Run_ShouldDisposeAndCancelWhenVerifiedSourceHasChanged()
    {
        using var fixture = new WorkerFixture();
        fixture.Source.SetupGet(source => source.CaptureSourceGeneration).Returns(2);

        await fixture.RunAsync();

        fixture.Source.Verify(source => source.DisposeAsync(), Times.Once);
        fixture.VerifyCancelled();
        fixture.Analyzer.Verify(analyzer => analyzer.AnalyzeAsync(
            It.IsAny<CaptureAnalysisRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyCheckpointCleared();
    }

    [TestMethod]
    public async Task Run_ShouldRecoverAlreadyCommittedResultWithoutInvokingAnalyzer()
    {
        using var fixture = new WorkerFixture();
        fixture.SetCommittedResult();

        await fixture.RunAsync();

        Assert.AreEqual(CaptureAnalyzerAttemptStatus.Succeeded, fixture.RecordedAttempts.Single().Status);
        fixture.Analyzer.Verify(analyzer => analyzer.AnalyzeAsync(
            It.IsAny<CaptureAnalysisRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.JobStore.Verify(store => store.TryCompleteAsync(
            fixture.Lease.LeaseToken,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldCancelWhenAnalyzerCancels()
    {
        using var fixture = new WorkerFixture
        {
            AnalyzerOutput = CaptureAnalyzerOutput.Cancelled,
        };

        await fixture.RunAsync();

        Assert.AreEqual(CaptureAnalyzerAttemptStatus.Cancelled, fixture.RecordedAttempts.Single().Status);
        fixture.VerifyCancelled();
        fixture.VerifyCheckpointCleared();
    }

    [TestMethod]
    public async Task Run_ShouldPreserveCheckpointWhenProcessCancellationInterruptsAnalyzer()
    {
        using var fixture = new WorkerFixture();
        fixture.Analyzer.Setup(analyzer => analyzer.AnalyzeAsync(
                It.IsAny<CaptureAnalysisRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                fixture.Cancellation.Cancel();
                return Task.FromResult(CaptureAnalyzerOutput.Cancelled);
            });

        await fixture.RunAsync();

        fixture.Checkpoint.Verify(checkpoint => checkpoint.ClearAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Run_ShouldConvertProviderExceptionToTransientRetry()
    {
        using var fixture = new WorkerFixture();
        fixture.Analyzer.Setup(analyzer => analyzer.AnalyzeAsync(
                It.IsAny<CaptureAnalysisRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider unavailable"));

        await fixture.RunAsync();

        Assert.AreEqual(CaptureAnalyzerAttemptStatus.TransientFailure, fixture.RecordedAttempts.Single().Status);
        fixture.JobStore.Verify(store => store.TryScheduleRetryAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure>(failure => failure.Code == AnalysisFailureCode.ProviderUnavailable),
            fixture.UtcNow.AddSeconds(30),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Log.Verify(log => log.LogException(
            It.IsAny<InvalidOperationException>(),
            "A Capture Analysis provider failed."), Times.Once);
        fixture.Checkpoint.Verify(checkpoint => checkpoint.ClearAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Run_ShouldPersistUnsupportedOutcomeAndFailTerminally()
    {
        using var fixture = new WorkerFixture
        {
            AnalyzerOutput = CaptureAnalyzerOutput.Unsupported(new AnalysisFailure(
                AnalysisFailureCode.UnsupportedMedia,
                AnalysisFailureDisposition.Terminal)),
        };

        await fixture.RunAsync();

        Assert.AreEqual(CaptureAnalyzerAttemptStatus.Unsupported, fixture.RecordedAttempts.Single().Status);
        fixture.Mutation.Verify(coordinator => coordinator.TryCommitCapabilityAsync(
            It.IsAny<AnalysisCommitToken>(),
            It.Is<CapabilityOutcome>(outcome => outcome.State == CapabilityOutcomeState.Unsupported),
            fixture.Snapshot!.DocumentRevision,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.JobStore.Verify(store => store.TryFailTerminalAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure>(failure => failure.Code == AnalysisFailureCode.UnsupportedMedia),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.VerifyCheckpointCleared();
    }

    [TestMethod]
    public async Task Run_ShouldRejectPayloadForDifferentCapability()
    {
        using var fixture = new WorkerFixture
        {
            AnalyzerOutput = CaptureAnalyzerOutput.Succeeded(new OcrDocumentV1(
                new PixelSize(100, 100),
                "text",
                [],
                [])),
        };

        await fixture.RunAsync();

        Assert.HasCount(2, fixture.RecordedAttempts);
        Assert.AreEqual(CaptureAnalyzerAttemptStatus.Succeeded, fixture.RecordedAttempts[0].Status);
        Assert.AreEqual(CaptureAnalyzerAttemptStatus.TerminalFailure, fixture.RecordedAttempts[1].Status);
        Assert.AreEqual(AnalysisFailureCode.InvalidResponse, fixture.RecordedAttempts[1].Failure?.Code);
    }

    [TestMethod]
    public async Task Run_ShouldCancelStaleCapabilityCommit()
    {
        using var fixture = new WorkerFixture();
        fixture.ResultCommitStatuses.Enqueue(CaptureAnalysisStoreWriteStatus.StaleCommit);

        await fixture.RunAsync();

        fixture.VerifyCancelled();
        fixture.JobStore.Verify(store => store.TryCompleteAsync(
            It.IsAny<CaptureAnalysisJobLeaseToken>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyCheckpointCleared();
    }

    [TestMethod]
    public async Task Run_ShouldRetryUnavailableCapabilityCommit()
    {
        using var fixture = new WorkerFixture();
        fixture.ResultCommitStatuses.Enqueue(CaptureAnalysisStoreWriteStatus.Unavailable);

        await fixture.RunAsync();

        Assert.HasCount(2, fixture.RecordedAttempts);
        Assert.AreEqual(CaptureAnalyzerAttemptStatus.TransientFailure, fixture.RecordedAttempts[1].Status);
        fixture.JobStore.Verify(store => store.TryScheduleRetryAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure>(failure => failure.Code == AnalysisFailureCode.InternalError),
            fixture.UtcNow.AddMinutes(1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldBoundCommitConflictRetries()
    {
        using var fixture = new WorkerFixture();
        fixture.ResultCommitStatuses.Enqueue(CaptureAnalysisStoreWriteStatus.Conflict);
        fixture.ResultCommitStatuses.Enqueue(CaptureAnalysisStoreWriteStatus.Conflict);
        fixture.ResultCommitStatuses.Enqueue(CaptureAnalysisStoreWriteStatus.Conflict);

        await fixture.RunAsync();

        fixture.Mutation.Verify(coordinator => coordinator.TryCommitCapabilityAsync(
            It.IsAny<AnalysisCommitToken>(),
            It.IsAny<CanonicalCapabilityResult>(),
            fixture.Snapshot!.DocumentRevision,
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        fixture.JobStore.Verify(store => store.TryScheduleRetryAsync(
            fixture.Lease.LeaseToken,
            It.IsAny<AnalysisFailure>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldStopAfterMaximumTransientAttempts()
    {
        using var fixture = new WorkerFixture
        {
            AnalyzerOutput = CaptureAnalyzerOutput.Failed(new AnalysisFailure(
                AnalysisFailureCode.Timeout,
                AnalysisFailureDisposition.Transient)),
        };
        fixture.SetInitialAttempts(Enumerable.Range(1, 8)
            .Select(number => fixture.CreateAttempt(
                number,
                CaptureAnalyzerAttemptStatus.TransientFailure))
            .ToArray());

        await fixture.RunAsync();

        Assert.HasCount(2, fixture.RecordedAttempts);
        Assert.AreEqual(CaptureAnalyzerAttemptStatus.TransientFailure, fixture.RecordedAttempts[0].Status);
        Assert.AreEqual(CaptureAnalyzerAttemptStatus.TerminalFailure, fixture.RecordedAttempts[1].Status);
        fixture.JobStore.Verify(store => store.TryFailTerminalAsync(
            fixture.Lease.LeaseToken,
            It.Is<AnalysisFailure>(failure =>
                failure.Code == AnalysisFailureCode.ProviderUnavailable &&
                failure.Disposition == AnalysisFailureDisposition.Terminal),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldIgnoreProjectionFailureAfterDurableCompletion()
    {
        using var fixture = new WorkerFixture();
        fixture.Projection.Setup(projection => projection.RefreshAsync(
                It.IsAny<CaptureId>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("projection unavailable"));

        await fixture.RunAsync();

        fixture.JobStore.Verify(store => store.TryCompleteAsync(
            fixture.Lease.LeaseToken,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Log.Verify(log => log.LogException(
            It.IsAny<IOException>(),
            "Failed to refresh a Capture Analysis projection."), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldRefreshCompletedProjectionDuringStartupRecovery()
    {
        using var fixture = new WorkerFixture();
        fixture.SetInitialAttempts(fixture.CreateAttempt(
            1,
            CaptureAnalyzerAttemptStatus.Succeeded));
        fixture.StartupIntents =
        [
            new CaptureAnalysisJobIntent(
                fixture.Intent.Key,
                CaptureAnalysisJobState.Completed,
                fixture.Intent.AttemptCount,
                fixture.Intent.EnqueuedAtUtc,
                nextAttemptAtUtc: null,
                latestFailure: null,
                fixture.Intent.Attempts),
        ];
        fixture.LeaseOnFirstPoll = false;

        await fixture.RunAsync();

        fixture.Projection.Verify(projection => projection.RefreshAsync(
            fixture.Intent.Key.CaptureId,
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(1, fixture.Reconciler.StartupCount);
    }

    [TestMethod]
    public async Task Run_ShouldRetryWaitingCapabilitiesDuringStartupRecovery()
    {
        using var fixture = new WorkerFixture
        {
            LeaseOnFirstPoll = false,
        };

        await fixture.RunAsync();

        fixture.JobStore.Verify(store => store.ResumeWaitingForCapabilityAsync(
            fixture.Descriptor.Capability,
            fixture.Descriptor.ProcessingBoundary,
            fixture.UtcNow,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldContinueWhenOneWaitingCapabilityCannotResume()
    {
        using var fixture = new WorkerFixture();
        var failure = new ArgumentException("invalid durable transition");
        fixture.JobStore.Setup(store => store.ResumeWaitingForCapabilityAsync(
                fixture.Descriptor.Capability,
                fixture.Descriptor.ProcessingBoundary,
                fixture.UtcNow,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<int>(failure));

        await fixture.RunAsync();

        fixture.JobStore.Verify(store => store.TryLeaseNextDueAsync(
            fixture.UtcNow,
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        fixture.JobStore.Verify(store => store.TryCompleteAsync(
            fixture.Lease.LeaseToken,
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Log.Verify(log => log.LogException(
            failure,
            "Failed to resume waiting Capture Analysis jobs."), Times.Once);
    }

    [TestMethod]
    public async Task Run_ShouldStopWhenAttemptRecordLosesLease()
    {
        using var fixture = new WorkerFixture
        {
            RecordAttemptStatus = CaptureAnalysisJobMutationStatus.LeaseLost,
        };

        await fixture.RunAsync();

        fixture.Mutation.Verify(coordinator => coordinator.TryCommitCapabilityAsync(
            It.IsAny<AnalysisCommitToken>(),
            It.IsAny<CanonicalCapabilityResult>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.JobStore.Verify(store => store.TryCompleteAsync(
            It.IsAny<CaptureAnalysisJobLeaseToken>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Run_ShouldNotDuplicateRecoveredSuccessAttempt()
    {
        using var fixture = new WorkerFixture();
        fixture.SetCommittedResult();
        fixture.SetInitialAttempts(fixture.CreateAttempt(
            1,
            CaptureAnalyzerAttemptStatus.Succeeded));

        await fixture.RunAsync();

        Assert.IsEmpty(fixture.RecordedAttempts);
        fixture.JobStore.Verify(store => store.TryCompleteAsync(
            fixture.Lease.LeaseToken,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void Host_ShouldNotStartWhenFeatureIsDisabled()
    {
        var worker = new Mock<ICaptureAnalysisWorker>();
        var cancellation = new Mock<ICancellationService>();
        var feature = new Mock<ICaptureAnalysisFeatureAvailability>();
        var log = new Mock<ILogService>();
        feature.SetupGet(value => value.IsCaptureAnalysisEnabled).Returns(false);
        using var host = new CaptureAnalysisWorkerHost(
            worker.Object,
            cancellation.Object,
            feature.Object,
            log.Object);

        host.Start();

        worker.Verify(value => value.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Host_ShouldStartOnlyOnceLogUnexpectedFailureAndCancelOnDispose()
    {
        var linkedCancellation = new CancellationTokenSource();
        var worker = new Mock<ICaptureAnalysisWorker>();
        var cancellation = new Mock<ICancellationService>();
        var feature = new Mock<ICaptureAnalysisFeatureAvailability>();
        var log = new Mock<ILogService>();
        var logged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken linkedToken = linkedCancellation.Token;
        feature.SetupGet(value => value.IsCaptureAnalysisEnabled).Returns(true);
        cancellation.Setup(value => value.GetLinkedCancellationTokenSource(
                It.IsAny<CancellationToken?>()))
            .Returns(linkedCancellation);
        worker.Setup(value => value.RunAsync(linkedToken))
            .ThrowsAsync(new InvalidOperationException("worker failed"));
        log.Setup(value => value.LogException(
                It.IsAny<InvalidOperationException>(),
                "Capture Analysis worker stopped unexpectedly."))
            .Callback(() => logged.TrySetResult(true));
        var host = new CaptureAnalysisWorkerHost(
            worker.Object,
            cancellation.Object,
            feature.Object,
            log.Object);

        host.Start();
        host.Start();
        await logged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.Dispose();

        worker.Verify(value => value.RunAsync(linkedToken), Times.Once);
        Assert.IsTrue(linkedToken.IsCancellationRequested);
    }

    private sealed class WorkerFixture : IDisposable
    {
        private readonly CaptureAnalysisAuthorizationScope _scope =
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope();
        private int _leasePollCount;

        public WorkerFixture()
        {
            UtcNow = new DateTimeOffset(2026, 8, 7, 20, 0, 0, TimeSpan.Zero);
            Preconditions = AnalysisTestData.CreatePreconditions(resolutionPolicyRevision: 2);
            var key = new CaptureAnalysisJobKey(
                Preconditions,
                AnalysisCapabilities.MediaPropertiesV1,
                ProcessingBoundary.OnDevice);
            Intent = CreateIntent(key, []);
            Lease = new CaptureAnalysisJobLease(
                CaptureAnalysisJobLeaseToken.New(),
                Intent,
                UtcNow.AddMinutes(2));
            AnalyzerIdentity = AnalysisTestData.CreateAnalyzer(
                analyzerId: "windows.media-properties");
            Descriptor = new CaptureAnalyzerDescriptor(
                key.Capability,
                AnalyzerIdentity,
                [CaptureMediaKind.Image],
                ProcessingBoundary.OnDevice,
                CaptureAnalyzerDataKind.None,
                CaptureAnalyzerRequirement.None,
                CaptureAnalyzerWorkloadClass.Lightweight,
                maximumSourceBytes: null,
                qualityTier: 1);
            Analyzer.SetupGet(value => value.Descriptor).Returns(Descriptor);
            var candidate = new CaptureAnalyzerCandidateEvaluation(
                Descriptor,
                CaptureAnalyzerEligibilityStatus.Eligible,
                CaptureAnalyzerAvailability.Available);
            ResolvedResolution = CaptureAnalyzerResolution.Resolved(Analyzer.Object, [candidate]);
            WaitingForPreparationResolution = CaptureAnalyzerResolution.WaitingForPreparation(
                [new CaptureAnalyzerCandidateEvaluation(
                    Descriptor,
                    CaptureAnalyzerEligibilityStatus.PreparationRequired,
                    CaptureAnalyzerAvailability.PreparationRequired)]);
            Resolutions =
            [
                ResolvedResolution,
                CaptureAnalyzerResolution.NoEligibleAnalyzer([]),
            ];
            AnalyzerOutput = CaptureAnalyzerOutput.Succeeded(new MediaPropertiesV1(
                CaptureMediaKind.Image,
                new PixelSize(1920, 1080),
                mimeType: "image/png"));
            Snapshot = new CaptureAnalysisStoreSnapshot(
                1,
                AnalysisTestData.CreateRecord(
                    Preconditions.SourceRevision,
                    AnalysisTestData.CreateRecipe()));

            ConfigureMocks();
        }

        public DateTimeOffset UtcNow { get; }
        public AnalysisCommitPreconditions Preconditions { get; }
        public CaptureAnalysisJobIntent Intent { get; private set; }
        public CaptureAnalysisJobLease Lease { get; private set; }
        public AnalyzerIdentity AnalyzerIdentity { get; }
        public CaptureAnalyzerDescriptor Descriptor { get; }
        public CaptureAnalyzerResolution ResolvedResolution { get; }
        public CaptureAnalyzerResolution WaitingForPreparationResolution { get; }
        public IReadOnlyList<CaptureAnalyzerResolution> Resolutions { get; set; }
        public CaptureAnalyzerOutput AnalyzerOutput { get; set; }
        public CanonicalCapabilityResult? DependencyResult { get; private set; }
        public CaptureAnalysisStoreSnapshot? Snapshot { get; set; }
        public long ResolutionPolicyRevision { get; set; } = 2;
        public CaptureAnalysisJobMutationStatus RecordAttemptStatus { get; set; } =
            CaptureAnalysisJobMutationStatus.Succeeded;
        public bool LeaseOnFirstPoll { get; set; } = true;
        public IReadOnlyList<CaptureAnalysisJobIntent> StartupIntents { get; set; } = [];
        public Queue<CaptureAnalysisStoreWriteStatus> ResultCommitStatuses { get; } = [];
        public HashSet<CaptureAnalysisAuthorizationStage> DeniedStages { get; } = [];
        public List<CaptureAnalyzerResolutionRequest> ResolutionRequests { get; } = [];
        public List<CaptureAnalyzerAttempt> RecordedAttempts { get; } = [];
        public CancellationTokenSource Cancellation { get; } = new();
        public Mock<ICaptureAnalysisJobStore> JobStore { get; } = new();
        public Mock<ICaptureAnalysisCheckpointStore> Checkpoints { get; } = new();
        public Mock<ICaptureAnalyzerCheckpoint> Checkpoint { get; } = new();
        public Mock<ICaptureAnalysisWakeWaiter> WakeWaiter { get; } = new();
        public Mock<ICaptureAnalyzerResolver> Resolver { get; } = new();
        public Mock<ICaptureAnalyzer> Analyzer { get; } = new();
        public Mock<ICaptureAnalyzerCatalog> AnalyzerCatalog { get; } = new();
        public Mock<ICaptureAnalysisPolicyService> Policy { get; } = new();
        public Mock<ICaptureAnalysisSourceVerifier> SourceVerifier { get; } = new();
        public Mock<IVerifiedCaptureAnalysisSource> Source { get; } = new();
        public Mock<ICaptureAnalysisMutationCoordinator> Mutation { get; } = new();
        public Mock<ICaptureAnalysisStore> Metadata { get; } = new();
        public Mock<ICaptureAnalysisFeatureAvailability> Feature { get; } = new();
        public Mock<ICaptureAnalysisProjectionRefresher> Projection { get; } = new();
        public RecordingReconciler Reconciler { get; } = new();
        public Mock<IClock> Clock { get; } = new();
        public Mock<ILogService> Log { get; } = new();

        public async Task RunAsync()
        {
            var worker = new CaptureAnalysisWorker(
                JobStore.Object,
                Checkpoints.Object,
                WakeWaiter.Object,
                Resolver.Object,
                AnalyzerCatalog.Object,
                Policy.Object,
                SourceVerifier.Object,
                Mutation.Object,
                Metadata.Object,
                Feature.Object,
                Projection.Object,
                Reconciler,
                Clock.Object,
                Log.Object);
            await worker.RunAsync(Cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void SetCommittedResult()
        {
            var result = new CanonicalCapabilityResult(
                Intent.Key.CaptureId,
                Intent.Key.SourceRevision,
                new MediaPropertiesV1(CaptureMediaKind.Image, new PixelSize(1920, 1080)),
                AnalyzerIdentity,
                ProcessingBoundary.OnDevice,
                UtcNow.AddSeconds(-1));
            Snapshot = new CaptureAnalysisStoreSnapshot(
                2,
                new CaptureAnalysisRecord(
                    Intent.Key.CaptureId,
                    CaptureMediaKind.Image,
                    AnalysisTestData.CapturedAtUtc,
                    Intent.Key.SourceRevision,
                    AnalysisTestData.CreateRecipe(),
                    [new CapabilityAnalysis(Intent.Key.Capability, result, null)]));
        }

        public void ConfigureDependencyInput()
        {
            CaptureAnalysisRecipe recipe = AnalysisTestData.CreateRecipe(
                capabilities:
                [
                    new RecipeCapability(
                        AnalysisCapabilities.OcrDocumentV1,
                        RecipeCapabilityRequirement.Required),
                    new RecipeCapability(
                        AnalysisCapabilities.MediaPropertiesV1,
                        RecipeCapabilityRequirement.Required,
                        [AnalysisCapabilities.OcrDocumentV1]),
                ]);
            DependencyResult = AnalysisTestData.CreateResult(
                new OcrDocumentV1(new PixelSize(100, 50), "dependency", [], []),
                AnalyzerIdentity,
                Preconditions.SourceRevision,
                UtcNow.AddSeconds(-1));
            Snapshot = new CaptureAnalysisStoreSnapshot(
                2,
                new CaptureAnalysisRecord(
                    Preconditions.CaptureId,
                    CaptureMediaKind.Image,
                    AnalysisTestData.CapturedAtUtc,
                    Preconditions.SourceRevision,
                    recipe,
                    [new CapabilityAnalysis(
                        AnalysisCapabilities.OcrDocumentV1,
                        DependencyResult,
                        latestOutcome: null)]));
            var key = new CaptureAnalysisJobKey(
                Preconditions,
                AnalysisCapabilities.MediaPropertiesV1,
                ProcessingBoundary.OnDevice,
                [AnalysisCapabilities.OcrDocumentV1]);
            Intent = CreateIntent(key, []);
            Lease = new CaptureAnalysisJobLease(
                Lease.LeaseToken,
                Intent,
                UtcNow.AddMinutes(2));
        }

        public void ConfigureMissingDependency()
        {
            CaptureAnalysisRecipe recipe = AnalysisTestData.CreateRecipe(
                capabilities:
                [
                    new RecipeCapability(
                        AnalysisCapabilities.OcrDocumentV1,
                        RecipeCapabilityRequirement.Required),
                    new RecipeCapability(
                        AnalysisCapabilities.MediaPropertiesV1,
                        RecipeCapabilityRequirement.Required,
                        [AnalysisCapabilities.OcrDocumentV1]),
                ]);
            Snapshot = new CaptureAnalysisStoreSnapshot(
                2,
                new CaptureAnalysisRecord(
                    Preconditions.CaptureId,
                    CaptureMediaKind.Image,
                    AnalysisTestData.CapturedAtUtc,
                    Preconditions.SourceRevision,
                    recipe));
            var key = new CaptureAnalysisJobKey(
                Preconditions,
                AnalysisCapabilities.MediaPropertiesV1,
                ProcessingBoundary.OnDevice,
                [AnalysisCapabilities.OcrDocumentV1]);
            Intent = CreateIntent(key, []);
            Lease = new CaptureAnalysisJobLease(
                Lease.LeaseToken,
                Intent,
                UtcNow.AddMinutes(2));
        }

        public void SetInitialAttempts(params CaptureAnalyzerAttempt[] attempts)
        {
            Intent = CreateIntent(Intent.Key, attempts);
            Lease = new CaptureAnalysisJobLease(
                Lease.LeaseToken,
                Intent,
                UtcNow.AddMinutes(2));
        }

        public CaptureAnalyzerAttempt CreateAttempt(
            int number,
            CaptureAnalyzerAttemptStatus status)
        {
            AnalysisFailure? failure = status switch
            {
                CaptureAnalyzerAttemptStatus.TransientFailure => new AnalysisFailure(
                    AnalysisFailureCode.Timeout,
                    AnalysisFailureDisposition.Transient),
                CaptureAnalyzerAttemptStatus.Unsupported => new AnalysisFailure(
                    AnalysisFailureCode.UnsupportedMedia,
                    AnalysisFailureDisposition.Terminal),
                CaptureAnalyzerAttemptStatus.TerminalFailure => new AnalysisFailure(
                    AnalysisFailureCode.ProviderUnavailable,
                    AnalysisFailureDisposition.Terminal),
                _ => null,
            };
            return new CaptureAnalyzerAttempt(
                number,
                AnalyzerIdentity,
                ProcessingBoundary.OnDevice,
                UtcNow.AddSeconds(number),
                UtcNow.AddSeconds(number + 1),
                status,
                failure);
        }

        public void VerifyCancelled()
        {
            JobStore.Verify(store => store.TryCancelAsync(
                Intent.Key,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        public void VerifyCheckpointCleared()
        {
            Checkpoint.Verify(checkpoint => checkpoint.ClearAsync(
                CancellationToken.None), Times.Once);
        }

        public void VerifyCaptureCheckpointsDeleted()
        {
            Checkpoints.Verify(store => store.DeleteCaptureAsync(
                Intent.Key.CaptureId,
                CancellationToken.None), Times.Once);
        }

        public void Dispose()
        {
            Cancellation.Dispose();
        }

        private void ConfigureMocks()
        {
            Checkpoints.Setup(store => store.Open(It.IsAny<CaptureAnalysisCheckpointKey>()))
                .Returns(Checkpoint.Object);
            Analyzer.SetupGet(value => value.Descriptor).Returns(Descriptor);
            Analyzer.Setup(value => value.AnalyzeAsync(
                    It.IsAny<CaptureAnalysisRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(AnalyzerOutput));
            AnalyzerCatalog.SetupGet(value => value.Analyzers).Returns([Analyzer.Object]);
            Feature.SetupGet(value => value.IsCaptureAnalysisEnabled).Returns(true);
            Feature.SetupGet(value => value.ResolutionPolicyRevision)
                .Returns(() => ResolutionPolicyRevision);
            Clock.SetupGet(value => value.UtcNow).Returns(UtcNow.UtcDateTime);

            Policy.Setup(value => value.AuthorizeAsync(
                    It.IsAny<CaptureAnalysisAuthorizationRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns((CaptureAnalysisAuthorizationRequest request, CancellationToken _) =>
                {
                    if (DeniedStages.Contains(request.Stage))
                    {
                        return ValueTask.FromResult(CaptureAnalysisAuthorizationDecision.Denied(
                            request,
                            CaptureAnalysisPolicyDenialReason.CapabilityNotAuthorized));
                    }

                    return ValueTask.FromResult(CaptureAnalysisAuthorizationDecision.Authorized(
                        request,
                        Preconditions.PolicyRevision,
                        Preconditions.ControlGeneration,
                        Preconditions.EnrollmentGeneration,
                        Preconditions.TombstoneGeneration,
                        _scope));
                });

            Resolver.Setup(value => value.ResolveAsync(
                    It.IsAny<CaptureAnalyzerResolutionRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns((CaptureAnalyzerResolutionRequest request, CancellationToken _) =>
                {
                    ResolutionRequests.Add(request);
                    int index = Math.Min(ResolutionRequests.Count - 1, Resolutions.Count - 1);
                    return ValueTask.FromResult(Resolutions[index]);
                });

            Metadata.Setup(value => value.GetAsync(
                    It.IsAny<CaptureId>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(Snapshot));
            Source.SetupGet(value => value.CaptureId).Returns(Preconditions.CaptureId);
            Source.SetupGet(value => value.MediaKind).Returns(CaptureMediaKind.Image);
            Source.SetupGet(value => value.CaptureSourceGeneration)
                .Returns(Preconditions.CaptureSourceGeneration);
            Source.SetupGet(value => value.SourceStamp).Returns(Preconditions.SourceStamp);
            Source.SetupGet(value => value.SourceRevision).Returns(Preconditions.SourceRevision);
            Source.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);
            SourceVerifier.Setup(value => value.TryOpenVerifiedAsync(
                    It.IsAny<CaptureAnalysisSourceVerificationRequest>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult<IVerifiedCaptureAnalysisSource?>(Source.Object));

            Mutation.Setup(value => value.TryCommitCapabilityAsync(
                    It.IsAny<AnalysisCommitToken>(),
                    It.IsAny<CanonicalCapabilityResult>(),
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(CreateWriteResult(
                    ResultCommitStatuses.Count > 0
                        ? ResultCommitStatuses.Dequeue()
                        : CaptureAnalysisStoreWriteStatus.Succeeded)));
            Mutation.Setup(value => value.TryCommitCapabilityAsync(
                    It.IsAny<AnalysisCommitToken>(),
                    It.IsAny<CapabilityOutcome>(),
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(CreateWriteResult(
                    CaptureAnalysisStoreWriteStatus.Succeeded)));

            JobStore.Setup(value => value.ReadAllAsync(It.IsAny<CancellationToken>()))
                .Returns(() => ToAsyncEnumerable(StartupIntents));
            JobStore.Setup(value => value.RecoverExpiredLeasesAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .Returns(ValueTask.FromResult(0));
            JobStore.Setup(value => value.ResumeWaitingForCapabilityAsync(
                    It.IsAny<CapabilityDefinition>(),
                    It.IsAny<ProcessingBoundary>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .Returns(ValueTask.FromResult(0));
            JobStore.Setup(value => value.TryLeaseNextDueAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult<CaptureAnalysisJobLease?>(
                    LeaseOnFirstPoll && _leasePollCount++ == 0 ? Lease : null));
            JobStore.Setup(value => value.GetNextDueTimeAsync(It.IsAny<CancellationToken>()))
                .Returns(ValueTask.FromResult<DateTimeOffset?>(null));
            JobStore.Setup(value => value.TryRecordAttemptAsync(
                    It.IsAny<CaptureAnalysisJobLeaseToken>(),
                    It.IsAny<CaptureAnalyzerAttempt>(),
                    It.IsAny<CancellationToken>()))
                .Returns((CaptureAnalysisJobLeaseToken _, CaptureAnalyzerAttempt attempt, CancellationToken _) =>
                {
                    if (RecordAttemptStatus != CaptureAnalysisJobMutationStatus.Succeeded)
                    {
                        return ValueTask.FromResult(new CaptureAnalysisJobMutationResult(
                            RecordAttemptStatus));
                    }

                    RecordedAttempts.Add(attempt);
                    Intent = new CaptureAnalysisJobIntent(
                        Intent.Key,
                        CaptureAnalysisJobState.Running,
                        Intent.AttemptCount + 1,
                        Intent.EnqueuedAtUtc,
                        nextAttemptAtUtc: null,
                        attempt.Failure,
                        [.. Intent.Attempts, attempt]);
                    return ValueTask.FromResult(new CaptureAnalysisJobMutationResult(
                        CaptureAnalysisJobMutationStatus.Succeeded,
                        Intent));
                });
            ConfigureJobTransitions();

            Projection.Setup(value => value.RefreshAsync(
                    It.IsAny<CaptureId>(),
                    It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            WakeWaiter.Setup(value => value.WaitAsync(
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Cancellation.Cancel();
                    return ValueTask.FromCanceled(Cancellation.Token);
                });
        }

        private void ConfigureJobTransitions()
        {
            JobStore.Setup(value => value.TryCompleteAsync(
                    It.IsAny<CaptureAnalysisJobLeaseToken>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(SucceededMutation(
                    CaptureAnalysisJobState.Completed,
                    latestFailure: null)));
            JobStore.Setup(value => value.TryCancelAsync(
                    It.IsAny<CaptureAnalysisJobKey>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromResult(SucceededMutation(
                    CaptureAnalysisJobState.Cancelled,
                    latestFailure: null)));
            JobStore.Setup(value => value.TryWaitForCapabilityAsync(
                    It.IsAny<CaptureAnalysisJobLeaseToken>(),
                    It.IsAny<AnalysisFailure?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((CaptureAnalysisJobLeaseToken _, AnalysisFailure? failure, CancellationToken _) =>
                    ValueTask.FromResult(SucceededMutation(
                        CaptureAnalysisJobState.WaitingForCapability,
                        failure)));
            JobStore.Setup(value => value.TryScheduleRetryAsync(
                    It.IsAny<CaptureAnalysisJobLeaseToken>(),
                    It.IsAny<AnalysisFailure>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .Returns((CaptureAnalysisJobLeaseToken _, AnalysisFailure failure,
                    DateTimeOffset nextAttemptAtUtc, CancellationToken _) =>
                    ValueTask.FromResult(SucceededMutation(
                        CaptureAnalysisJobState.RetryScheduled,
                        failure,
                        nextAttemptAtUtc)));
            JobStore.Setup(value => value.TryFailTerminalAsync(
                    It.IsAny<CaptureAnalysisJobLeaseToken>(),
                    It.IsAny<AnalysisFailure>(),
                    It.IsAny<CancellationToken>()))
                .Returns((CaptureAnalysisJobLeaseToken _, AnalysisFailure failure, CancellationToken _) =>
                    ValueTask.FromResult(SucceededMutation(
                        CaptureAnalysisJobState.TerminalFailure,
                        failure)));
        }

        private CaptureAnalysisJobMutationResult SucceededMutation(
            CaptureAnalysisJobState state,
            AnalysisFailure? latestFailure,
            DateTimeOffset? nextAttemptAtUtc = null)
        {
            return new CaptureAnalysisJobMutationResult(
                CaptureAnalysisJobMutationStatus.Succeeded,
                new CaptureAnalysisJobIntent(
                    Intent.Key,
                    state,
                    Intent.AttemptCount,
                    Intent.EnqueuedAtUtc,
                    nextAttemptAtUtc,
                    latestFailure,
                    Intent.Attempts));
        }

        private CaptureAnalysisStoreWriteResult CreateWriteResult(
            CaptureAnalysisStoreWriteStatus status)
        {
            return status == CaptureAnalysisStoreWriteStatus.Succeeded
                ? new CaptureAnalysisStoreWriteResult(status, Snapshot!)
                : new CaptureAnalysisStoreWriteResult(status);
        }

        private CaptureAnalysisJobIntent CreateIntent(
            CaptureAnalysisJobKey key,
            IReadOnlyList<CaptureAnalyzerAttempt> attempts)
        {
            return new CaptureAnalysisJobIntent(
                key,
                CaptureAnalysisJobState.Running,
                attempts.Count,
                UtcNow.AddMinutes(-1),
                nextAttemptAtUtc: null,
                attempts.LastOrDefault()?.Failure,
                attempts);
        }

        private static async IAsyncEnumerable<CaptureAnalysisJobIntent> ToAsyncEnumerable(
            IEnumerable<CaptureAnalysisJobIntent> intents)
        {
            foreach (CaptureAnalysisJobIntent intent in intents)
            {
                yield return intent;
            }

            await Task.CompletedTask;
        }

        internal sealed class RecordingReconciler : ICaptureAnalysisReconciler
        {
            public int StartupCount { get; private set; }

            public int PendingChangesCount { get; private set; }

            public Task ReconcileStartupAsync(CancellationToken cancellationToken = default)
            {
                StartupCount++;
                return Task.CompletedTask;
            }

            public Task ConsumePendingChangesAsync(CancellationToken cancellationToken = default)
            {
                PendingChangesCount++;
                return Task.CompletedTask;
            }
        }
    }
}
