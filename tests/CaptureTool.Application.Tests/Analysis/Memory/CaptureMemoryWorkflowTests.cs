using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Intake;
using CaptureTool.Application.Abstractions.Analysis.Maintenance;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Analysis.Maintenance;
using CaptureTool.Application.Analysis.Memory;
using CaptureTool.Application.Tests.Analysis.Domain;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using Moq;

namespace CaptureTool.Application.Tests.Analysis.Memory;

[TestClass]
public sealed class CaptureMemoryWorkflowTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Enable_ShouldPersistIntentBeforeGrantAndUseOnePreparationBackfillWorkflow(bool includeExisting, bool unavailable)
    {
        using var fixture = new Fixture();
        if (unavailable)
        {
            fixture.Preparation.Setup(value => value.PrepareAsync(It.IsAny<AnalysisCapabilityPreparationRequest>(),
                    It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AnalysisCapabilityPreparationState.Unsupported(new AnalysisFailure(
                    AnalysisFailureCode.CapabilityUnavailable, AnalysisFailureDisposition.Terminal)));
        }

        CaptureMemoryOperation result = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable, includeExisting));

        Assert.AreEqual(unavailable ? CaptureMemoryOperationStatus.Partial : CaptureMemoryOperationStatus.Succeeded, result.Status);
        Assert.IsTrue(fixture.Current.IsProcessingAuthorized);
        Assert.AreEqual(result.Id, fixture.Store.Snapshot.Operation!.Id);
        Assert.AreEqual("accepted", fixture.Ordering[0]);
        Assert.AreEqual("grant", fixture.Ordering[1]);
        fixture.Backfill.Verify(value => value.RunAsync(It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
            It.IsAny<CancellationToken>()), includeExisting ? Times.Once() : Times.Never());
    }

    [TestMethod]
    public async Task ActiveOperation_ShouldRejectOverlapAndCancelByExactIdentity()
    {
        using var fixture = new Fixture();
        TaskCompletionSource started = fixture.BlockPreparation();
        Task<CaptureMemoryOperation> enable = fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid id = fixture.Store.Snapshot.Operation!.Id;
        CaptureMemoryOperation overlapping = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.RebuildSearch));
        Assert.AreEqual(CaptureMemoryOperationStatus.Conflict, overlapping.Status);
        Assert.AreEqual(id, fixture.Store.Snapshot.Operation.Id);
        fixture.Workflow.Cancel(Guid.NewGuid());
        Assert.IsFalse(enable.IsCompleted);

        fixture.Workflow.Cancel(id);
        Assert.AreEqual(CaptureMemoryOperationStatus.Cancelled, (await enable.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        Assert.IsTrue(fixture.Current.IsProcessingAuthorized, "Cancelling preparation does not silently revoke consent.");
    }

    [TestMethod]
    public async Task TurnOff_ShouldPreemptPreparationAndLeaveConsentOff()
    {
        using var fixture = new Fixture();
        TaskCompletionSource started = fixture.BlockPreparation();
        Task<CaptureMemoryOperation> enable = fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable, true));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var erased = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.TurnOffAndErase));

        Assert.AreEqual(CaptureMemoryOperationStatus.Cancelled, (await enable).Status);
        Assert.AreEqual(CaptureMemoryOperationStatus.Succeeded, erased.Status);
        Assert.IsFalse(fixture.Current.IsProcessingAuthorized);
        fixture.Backfill.Verify(value => value.RunAsync(It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [TestMethod]
    public async Task TurnOff_ShouldNotWaitForAProviderThatIgnoresCancellation()
    {
        using var fixture = new Fixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new TaskCompletionSource<AnalysisCapabilityPreparationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Preparation.Setup(value => value.PrepareAsync(It.IsAny<AnalysisCapabilityPreparationRequest>(),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                started.TrySetResult();
                return provider.Task;
            });
        Task<CaptureMemoryOperation> enable = fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        CaptureMemoryOperation erased = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.TurnOffAndErase))
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(CaptureMemoryOperationStatus.Succeeded, erased.Status);
        Assert.IsFalse(fixture.Current.IsProcessingAuthorized);
        provider.SetResult(AnalysisCapabilityPreparationState.Ready(AnalysisTestData.CreateAnalyzer(), ProcessingBoundary.OnDevice));
        Assert.AreEqual(CaptureMemoryOperationStatus.Cancelled, (await enable.WaitAsync(TimeSpan.FromSeconds(5))).Status);
        Assert.AreEqual(erased.Id, fixture.Store.Snapshot.Operation!.Id);
    }

    [TestMethod]
    public async Task FailedFinalJournalWrite_ShouldExposeRecoveryAndRetrySameIdentity()
    {
        using var fixture = new Fixture();
        fixture.Store.RejectFinishedWrites = true;
        CaptureMemoryOperation first = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable));
        Assert.AreEqual(CaptureMemoryOperationStatus.RecoveryRequired, first.Status);
        Guid id = fixture.Store.Snapshot.Operation!.Id;
        CaptureMemoryWorkflowSnapshot snapshot = await fixture.Workflow.GetCurrentAsync();
        Assert.IsFalse(snapshot.IsBusy);
        Assert.AreEqual(CaptureMemoryOperationStatus.RecoveryRequired, snapshot.Operation!.Status);

        fixture.Store.RejectFinishedWrites = false;
        CaptureMemoryOperation retried = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable));
        Assert.AreEqual(id, retried.Id);
        Assert.AreEqual(CaptureMemoryOperationStatus.Succeeded, retried.Status);
        fixture.Commands.Verify(value => value.ApplyConsentDecisionAsync(It.IsAny<CaptureAnalysisConsentResponse>(),
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Restart_ShouldResumeTheSameIntentButNeverRegrantAfterRevocation(bool revoked)
    {
        using var fixture = new Fixture();
        TaskCompletionSource started = fixture.BlockPreparation();
        Task<CaptureMemoryOperation> enable = fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable, true));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Guid id = fixture.Store.Snapshot.Operation!.Id;
        fixture.Workflow.Dispose();
        Assert.IsTrue((await enable.WaitAsync(TimeSpan.FromSeconds(5))).IsRunning);
        fixture.SetReadyPreparation();
        if (revoked) { fixture.SetPolicy(fixture.Current.Policy!.Revoke()); }
        using CaptureMemoryWorkflow restarted = fixture.CreateWorkflow();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        restarted.Changed += (_, _) =>
        {
            if (fixture.Store.Snapshot.Operation?.IsRunning == false) { finished.TrySetResult(); }
        };

        await restarted.ResumeAsync();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(id, fixture.Store.Snapshot.Operation!.Id);
        Assert.AreEqual(revoked ? CaptureMemoryOperationStatus.Conflict : CaptureMemoryOperationStatus.Succeeded,
            fixture.Store.Snapshot.Operation.Status);
        fixture.Commands.Verify(value => value.ApplyConsentDecisionAsync(It.IsAny<CaptureAnalysisConsentResponse>(),
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [TestMethod]
    public async Task JournalConflict_ShouldPreventConsentOrModelSideEffects()
    {
        using var fixture = new Fixture();
        fixture.Store.RejectWrites = true;

        var result = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable));

        Assert.AreEqual(CaptureMemoryOperationStatus.Conflict, result.Status);
        fixture.Commands.VerifyNoOtherCalls();
        fixture.Preparation.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ObserverFailure_ShouldNotInterruptDurableWorkflow()
    {
        using var fixture = new Fixture();
        fixture.Workflow.Changed += (_, _) => throw new InvalidOperationException("Detached UI observer");
        var result = await fixture.Workflow.ExecuteAsync(new(CaptureMemoryOperationKind.Enable));
        Assert.AreEqual(CaptureMemoryOperationStatus.Succeeded, result.Status);
    }

    private sealed class Fixture : IDisposable
    {
        public List<string> Ordering { get; } = [];
        public MemoryOperationStore Store { get; }
        public Mock<ICaptureAnalysisPolicyService> Policy { get; } = new();
        public Mock<ICaptureAnalysisPolicyCommandService> Commands { get; } = new(MockBehavior.Strict);
        public Mock<IUserInitiatedAnalysisCapabilityPreparationService> Preparation { get; } = new(MockBehavior.Strict);
        public Mock<ICaptureAnalysisBackfillService> Backfill { get; } = new(MockBehavior.Strict);
        public CaptureAnalysisPolicySnapshot Current { get; private set; }
        public CaptureMemoryWorkflow Workflow { get; }

        public Fixture()
        {
            Store = new(Ordering);
            Current = new(CaptureAnalysisPolicySnapshotStatus.Available, CaptureAnalysisConsentState.Unknown,
                new(1, new(CaptureAnalysisPolicy.Unknown, [])));
            Policy.Setup(value => value.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Current);
            Commands.Setup(value => value.ApplyConsentDecisionAsync(It.IsAny<CaptureAnalysisConsentResponse>(),
                    It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Ordering.Add("grant");
                    Assert.IsNotNull(Store.Snapshot.Operation);
                    SetPolicy(Current.Policy!.GrantFutureCaptures(CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(), 10));
                    return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(CaptureAnalysisPolicyChangeStatus.Succeeded, Current));
                });
            Commands.Setup(value => value.AuthorizeExistingCaptureBackfillAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    SetPolicy(Current.Policy!.AuthorizeExistingCaptureBackfill(10));
                    return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(CaptureAnalysisPolicyChangeStatus.Succeeded, Current));
                });
            Commands.Setup(value => value.RevokeAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    SetPolicy(Current.Policy!.Revoke());
                    return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(CaptureAnalysisPolicyChangeStatus.Succeeded, Current));
                });
            SetReadyPreparation();
            Backfill.Setup(value => value.RunAsync(It.IsAny<IProgress<CaptureAnalysisBackfillProgress>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    SetPolicy(Current.Policy!.StartExistingCaptureBackfill().AdvanceExistingCaptureBackfill(10));
                    return Task.FromResult(new CaptureAnalysisBackfillRunResult(CaptureAnalysisBackfillRunStatus.Completed,
                        new CaptureAnalysisBackfillProgress(10, 10, 1)));
                });
            Workflow = CreateWorkflow();
        }

        public CaptureMemoryWorkflow CreateWorkflow() => new(Policy.Object, Commands.Object,
            Mock.Of<ICaptureAnalysisMaintenanceService>(MockBehavior.Strict), Preparation.Object, Backfill.Object,
            new Cleanup(), Store, Mock.Of<IClock>(value => value.UtcNow == DateTime.UtcNow), Mock.Of<ILogService>());

        public void SetPolicy(CaptureAnalysisPolicy policy) => Current = new(CaptureAnalysisPolicySnapshotStatus.Available,
            policy.ConsentState, new(Current.ControlDocumentRevision + 1, new(policy, [])));

        public void SetReadyPreparation() => Preparation.Setup(value => value.PrepareAsync(It.IsAny<AnalysisCapabilityPreparationRequest>(),
                It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AnalysisCapabilityPreparationState.Ready(AnalysisTestData.CreateAnalyzer(), ProcessingBoundary.OnDevice));

        public TaskCompletionSource BlockPreparation()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Preparation.Setup(value => value.PrepareAsync(It.IsAny<AnalysisCapabilityPreparationRequest>(),
                    It.IsAny<IProgress<AnalysisCapabilityPreparationProgress>>(), It.IsAny<CancellationToken>()))
                .Returns<AnalysisCapabilityPreparationRequest, IProgress<AnalysisCapabilityPreparationProgress>?, CancellationToken>(async (_, _, token) =>
                {
                    started.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    throw new InvalidOperationException("Unreachable");
                });
            return started;
        }

        public void Dispose() => Workflow.Dispose();
    }

    private sealed class MemoryOperationStore(List<string> ordering) : ICaptureMemoryOperationStore
    {
        public CaptureMemoryOperationSnapshot Snapshot { get; private set; } = new(0, null);
        public bool RejectWrites;
        public bool RejectFinishedWrites;
        public ValueTask<CaptureMemoryOperationSnapshot> GetAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);
        public ValueTask<bool> TryWriteAsync(CaptureMemoryOperation operation, long expectedRevision, CancellationToken cancellationToken = default)
        {
            if (RejectWrites || RejectFinishedWrites && operation.Phase == CaptureMemoryOperationPhase.Finished ||
                Snapshot.Revision != expectedRevision) { return ValueTask.FromResult(false); }
            Snapshot = new(expectedRevision + 1, operation);
            if (operation.Phase == CaptureMemoryOperationPhase.Accepted) { ordering.Add("accepted"); }
            return ValueTask.FromResult(true);
        }
    }

    private sealed class Cleanup : ICaptureAnalysisCleanupCoordinator
    {
        public ValueTask<bool> ReconcileAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
        public ValueTask<bool> ReconcileCaptureAsync(CaptureId id, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
    }
}
