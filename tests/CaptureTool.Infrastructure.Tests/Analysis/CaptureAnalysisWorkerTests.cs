using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Persistence;
using CaptureTool.Infrastructure.CaptureAssets;
using CaptureTool.Domain.Capture;
using System.Collections.Concurrent;

namespace CaptureTool.Infrastructure.Tests.Analysis;

[TestClass]
public sealed class CaptureAnalysisWorkerTests
{
    public TestContext TestContext { get; set; } = null!;
    private CancellationToken Ct => TestContext.CancellationToken;

    [TestMethod]
    public async Task WorkerHonorsFifoStepAndFallbackOrderAndReportsPreparationSeparately()
    {
        using var fixture = new Fixture();
        fixture.Preferred.Ready = AnalyzerAvailability.PreparationRequired;
        fixture.Preferred.Execute = (_, _) => Task.FromResult(AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "provider-error"));
        AnalysisRequest first = await fixture.EnqueueAsync(Ct);
        AnalysisRequest second = await fixture.EnqueueAsync(Ct);
        var progress = new ConcurrentQueue<AnalysisActivity>();
        fixture.Worker.ProgressChanged += _ => throw new InvalidOperationException("An observer failed.");
        fixture.Worker.ProgressChanged += value => progress.Enqueue(value.Activity);
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(new[] { "prepare:preferred", "preferred", "fallback", "description", "preferred", "fallback", "description" }, fixture.Calls.ToArray());
        Assert.Contains(AnalysisActivity.Preparing, progress.ToArray());
        Assert.Contains(AnalysisActivity.Analyzing, progress.ToArray());
        Assert.AreEqual(AnalysisActivity.Idle, fixture.Worker.Progress.Activity);
        Assert.AreEqual(AnalysisRunStatus.Completed, fixture.Worker.Progress.LastRunStatus);
        Assert.IsFalse(fixture.Worker.Progress.LastRunHadFailures, "A successful fallback resolves the preferred model's failure.");
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(first.CaptureId, Ct))!.Run.Status);
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(second.CaptureId, Ct))!.Run.Status);
        Assert.IsTrue((await fixture.Store.GetAsync(first.CaptureId, cancellationToken: Ct))!.Results.All(result => result.ProducingRunId == first.RequestId));
    }

    [TestMethod]
    [DataRow(AnalyzerOutcomeKind.ContentRejected, true)]
    [DataRow(AnalyzerOutcomeKind.InvalidSource, false)]
    [DataRow(AnalyzerOutcomeKind.Cancelled, false)]
    public async Task RefusalCancellationAndInvalidSourceNeverTriggerFallback(AnalyzerOutcomeKind kind, bool continues)
    {
        using var fixture = new Fixture();
        fixture.Preferred.Execute = (_, _) => Task.FromResult(AnalyzerOutcome.Unsuccessful(kind, "terminal"));
        await fixture.EnqueueAsync(Ct);
        await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.DoesNotContain("fallback", fixture.Calls.ToArray());
        Assert.AreEqual(2, fixture.Calls.Count(call => call == "preferred"));
        Assert.AreEqual(continues ? 2 : 0, fixture.Calls.Count(call => call == "description"));
    }

    [TestMethod]
    public async Task EmptySuccessDoesNotFallBackAndUnsupportedStepsDoNotBlockLaterWork()
    {
        using var fixture = new Fixture();
        fixture.Description.Ready = AnalyzerAvailability.Unsupported;
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(new[] { "preferred" }, fixture.Calls.ToArray());
        AnalysisWorkItem work = (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!;
        Assert.AreEqual(AnalysisRunStatus.Completed, work.Run.Status);
        Assert.AreEqual(AnalyzerOutcomeKind.Unsupported, work.Run.CompletedSteps[1].Outcome);
        var text = (TextRecognitionMetadata)(await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results.Single().Payload;
        Assert.IsEmpty(text.Regions);
        Assert.IsFalse(fixture.Worker.Progress.LastRunHadFailures, "Unsupported capabilities are ordinary skips.");
    }

    [TestMethod]
    [DataRow(AnalyzerOutcomeKind.Failed)]
    [DataRow(AnalyzerOutcomeKind.TemporarilyUnavailable)]
    public async Task ExhaustedModelsReportFailedStepsEvenWhenAllStepsAreCompleted(AnalyzerOutcomeKind outcome)
    {
        using var fixture = new Fixture();
        foreach (var analyzer in new[] { fixture.Preferred, fixture.Fallback, fixture.Description })
            analyzer.Execute = (_, _) => Task.FromResult(AnalyzerOutcome.Unsuccessful(outcome, "provider-error"));
        var request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        var record = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.IsEmpty(record.Results);
        Assert.AreEqual(AnalysisRunStatus.Completed, fixture.Worker.Progress.LastRunStatus);
        Assert.IsTrue(fixture.Worker.Progress.LastRunHadFailures);
        Assert.AreEqual(AnalysisActivity.Idle, fixture.Worker.Progress.Activity);
    }

    [TestMethod]
    [DataRow(AnalyzerOutcomeKind.Failed)]
    [DataRow(AnalyzerOutcomeKind.TemporarilyUnavailable)]
    public async Task UnsupportedFallbackCannotHideExecutionFailureAndLaterResultsAreRetained(AnalyzerOutcomeKind outcome)
    {
        using var fixture = new Fixture();
        fixture.Preferred.Execute = (_, _) => Task.FromResult(AnalyzerOutcome.Unsuccessful(outcome, "provider-error"));
        fixture.Fallback.Ready = AnalyzerAvailability.Unsupported;
        var request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        var work = (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!;
        Assert.AreEqual(outcome, work.Run.CompletedSteps[0].Outcome);
        Assert.AreEqual("provider-error", work.Run.CompletedSteps[0].FailureCode);
        Assert.AreEqual(AnalysisRunStatus.Completed, work.Run.Status);
        Assert.IsTrue(fixture.Worker.Progress.LastRunHadFailures);
        var record = (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!;
        Assert.AreEqual(AnalysisCapability.Description, record.Results.Single().Payload.Capability);
    }

    [TestMethod]
    public async Task LanguageCompatibilitySkipsAnIncompatiblePreferredModel()
    {
        using var fixture = new Fixture();
        fixture.Preferred.UnsupportedLanguage = "de";
        AnalysisRequest request = await fixture.EnqueueAsync(Ct, "de");
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(new[] { "fallback", "description" }, fixture.Calls.ToArray());
        Assert.AreEqual("fallback", (await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!
            .Results.Single(result => result.Payload.Capability == AnalysisCapability.TextRecognition).Producer.AnalyzerId);
    }

    [TestMethod]
    public async Task RestartSkipsCommittedStepAndRetainsRunIdentity()
    {
        using var fixture = new Fixture();
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        AnalysisWorkItem work = (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!;
        await fixture.Store.BindSourceAsync(work.Token, AnalysisTestEnvironment.Revision(), Ct);
        await fixture.Store.CommitStepAsync(work.Token, new(AnalysisCapability.TextRecognition, AnalyzerOutcomeKind.Succeeded, null),
            new(new TextRecognitionMetadata([]), new("preferred", "fake", "model", "1"), DateTimeOffset.UtcNow, "v1", request.RequestId), Ct);
        using LocalCaptureAnalysisStore reopened = fixture.Environment.CreateStore();
        using var restartedWorker = new CaptureAnalysisWorker(reopened, fixture.Authorization, fixture.Source, fixture.Configuration, fixture.Adapters, fixture.Catalog);
        await DrainAsync(restartedWorker, Ct);
        CollectionAssert.AreEqual(new[] { "description" }, fixture.Calls.ToArray());
        AnalysisWorkItem completed = (await reopened.GetWorkAsync(request.CaptureId, Ct))!;
        Assert.AreEqual(request.RequestId, completed.Run.Id);
        Assert.AreEqual(AnalysisRunStatus.Completed, completed.Run.Status);
    }

    [TestMethod]
    public async Task MutationBeforePublicationAbortsWithoutWritingMetadata()
    {
        using var fixture = new Fixture();
        fixture.Source.Unchanged = false;
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(AnalysisRunStatus.InvalidSource, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
        Assert.IsEmpty((await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results);
        Assert.AreEqual(1, fixture.Source.Disposals);
        CollectionAssert.AreEqual(new[] { "preferred" }, fixture.Calls.ToArray());
    }

    [TestMethod]
    public async Task MissingSourceDoesNotBlockNextCapture()
    {
        using var fixture = new Fixture();
        AnalysisRequest first = await fixture.EnqueueAsync(Ct);
        AnalysisRequest second = await fixture.EnqueueAsync(Ct);
        fixture.Source.FailPath = first.SourcePath;
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(AnalysisRunStatus.InvalidSource, (await fixture.Store.GetWorkAsync(first.CaptureId, Ct))!.Run.Status);
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(second.CaptureId, Ct))!.Run.Status);
    }

    [TestMethod]
    public async Task CooperativeTimeoutFallsBackWithinTheConfiguredBudget()
    {
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(40));
        fixture.Preferred.Execute = async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new AssertFailedException("The attempt must time out.");
        };
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        CollectionAssert.AreEqual(new[] { "preferred", "fallback", "description" }, fixture.Calls.ToArray());
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    [TestMethod]
    public async Task ConcurrentProgressCannotDeliverLoadingAfterIdleAndLateCallbacksAreIgnored()
    {
        using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseObserver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new ConcurrentQueue<AnalysisActivitySnapshot>();
        fixture.Preferred.Execute = (_, _) => { entered.TrySetResult(); return result.Task; };
        fixture.Worker.ProgressChanged += value =>
        {
            if (value.Fraction == 0.5)
            {
                reporting.TrySetResult();
                releaseObserver.Task.Wait(Ct);
            }
        };
        fixture.Worker.ProgressChanged += observed.Enqueue;
        await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        Task callback = Task.CompletedTask;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            IProgress<AnalysisProgress> progress = fixture.Preferred.Progress!;
            callback = Task.Run(() => progress.Report(new(AnalysisProgressStage.Analyzing, 0.5)), Ct);
            await reporting.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            result.SetResult(fixture.Preferred.Success());
            // The snapshot can reach idle while an earlier notification is still being delivered.
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            releaseObserver.SetResult();
            await callback.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            AnalysisActivitySnapshot[] notifications = observed.ToArray();
            Assert.AreEqual(AnalysisActivity.Idle, notifications[^1].Activity);
            Assert.IsTrue(notifications.SkipWhile(value => value.Activity != AnalysisActivity.Idle)
                .All(value => value.Activity == AnalysisActivity.Idle));

            int count = observed.Count;
            progress.Report(new(AnalysisProgressStage.Analyzing, 0.9));
            fixture.Description.Progress!.Report(new(AnalysisProgressStage.Preparing));
            Assert.HasCount(count, observed);
            Assert.AreEqual(AnalysisActivity.Idle, fixture.Worker.Progress.Activity);
        }
        finally
        {
            releaseObserver.TrySetResult();
            result.TrySetResult(fixture.Preferred.Success());
            shutdown.Cancel();
            await Task.WhenAll(callback, runner).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NoncooperativeTimeoutPreservesBacklogAndResumesInOrderWhenProviderExits(bool faults)
    {
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(40));
        var late = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        AnalysisRequest first = await fixture.EnqueueAsync(Ct);
        AnalysisRequest second = await fixture.EnqueueAsync(Ct);
        AnalysisRequest third = await fixture.EnqueueAsync(Ct);
        fixture.Preferred.Execute = (input, _) => input.CaptureId == first.CaptureId ? late.Task : Task.FromResult(fixture.Preferred.Success());
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            CollectionAssert.AreEqual(new[] { "preferred" }, fixture.Calls.ToArray());
            Assert.AreEqual(AnalysisRunStatus.Failed, (await fixture.Store.GetWorkAsync(first.CaptureId, Ct))!.Run.Status);
            Assert.AreEqual(2, fixture.Worker.Progress.QueuedCaptures);
            Assert.AreEqual("provider-not-stopped", fixture.Worker.Progress.FailureCode);
            foreach (AnalysisRequest queued in new[] { second, third })
            {
                AnalysisWorkItem work = (await fixture.Store.GetWorkAsync(queued.CaptureId, Ct))!;
                Assert.AreEqual(AnalysisRunStatus.Queued, work.Run.Status);
                Assert.IsNull(work.Run.SourceRevision);
                Assert.IsEmpty(work.Run.CompletedSteps);
            }
            IProgress<AnalysisProgress> staleProgress = fixture.Preferred.Progress!;
            staleProgress.Report(new(AnalysisProgressStage.Analyzing, 0.9));
            Assert.AreEqual(AnalysisActivity.ProviderUnavailable, fixture.Worker.Progress.Activity);
            if (faults) late.SetException(new InvalidOperationException("Late provider failure."));
            else late.SetResult(fixture.Preferred.Success());
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            CollectionAssert.AreEqual(new[] { "preferred", "preferred", "description", "preferred", "description" }, fixture.Calls.ToArray());
            Assert.IsEmpty((await fixture.Store.GetAsync(first.CaptureId, cancellationToken: Ct))!.Results);
            foreach (AnalysisRequest queued in new[] { second, third })
            {
                AnalysisWorkItem work = (await fixture.Store.GetWorkAsync(queued.CaptureId, Ct))!;
                Assert.AreEqual(queued.RequestId, work.Run.Id);
                Assert.AreEqual(AnalysisRunStatus.Completed, work.Run.Status);
            }
        }
        finally
        {
            late.TrySetResult(fixture.Preferred.Success());
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
    }

    [TestMethod]
    public async Task CancelAndClearRemainResponsiveWhileProviderIsUnavailable()
    {
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(40));
        var late = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        AnalysisRequest first = await fixture.EnqueueAsync(Ct);
        AnalysisRequest second = await fixture.EnqueueAsync(Ct);
        await fixture.EnqueueAsync(Ct);
        fixture.Preferred.Execute = (input, _) => input.CaptureId == first.CaptureId ? late.Task : Task.FromResult(fixture.Preferred.Success());
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            await fixture.Worker.CancelAsync(second.CaptureId, Ct);
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable && value.QueuedCaptures == 1, Ct);
            Assert.AreEqual(AnalysisRunStatus.Cancelled, (await fixture.Store.GetWorkAsync(second.CaptureId, Ct))!.Run.Status);
            await fixture.Worker.ClearAsync(12, Ct);
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            Assert.IsEmpty(await fixture.Store.ReadAllAsync(Ct));
            Assert.IsEmpty(await fixture.Store.ReadPendingAsync(Ct));

            AnalysisRequest fresh = await fixture.EnqueueAsync(Ct);
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            CollectionAssert.AreEqual(new[] { "preferred" }, fixture.Calls.ToArray());
            late.SetResult(fixture.Preferred.Success());
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            Assert.IsNull(await fixture.Store.GetWorkAsync(first.CaptureId, Ct));
            Assert.IsNull(await fixture.Store.GetWorkAsync(second.CaptureId, Ct));
            Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(fresh.CaptureId, Ct))!.Run.Status);
            Assert.AreEqual(fresh.CaptureId, (await fixture.Store.ReadAllAsync(Ct)).Single().CaptureId);
        }
        finally
        {
            late.TrySetResult(fixture.Preferred.Success());
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
    }

    [TestMethod]
    public async Task RevocationCancelsWaitingBacklogWithoutWaitingForProviderExit()
    {
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(40));
        var late = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        AnalysisRequest first = await fixture.EnqueueAsync(Ct);
        AnalysisRequest second = await fixture.EnqueueAsync(Ct);
        fixture.Preferred.Execute = (input, _) => input.CaptureId == first.CaptureId ? late.Task : Task.FromResult(fixture.Preferred.Success());
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            await fixture.Authorization.ChangeAsync(false, Ct);
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            Assert.AreEqual(AnalysisRunStatus.Cancelled, (await fixture.Store.GetWorkAsync(second.CaptureId, Ct))!.Run.Status);
            await fixture.Authorization.ChangeAsync(true, Ct);
            AnalysisRequest fresh = await fixture.EnqueueAsync(Ct);
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            CollectionAssert.AreEqual(new[] { "preferred" }, fixture.Calls.ToArray());
            late.SetResult(fixture.Preferred.Success());
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            Assert.AreEqual(AnalysisRunStatus.Cancelled, (await fixture.Store.GetWorkAsync(second.CaptureId, Ct))!.Run.Status);
            Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(fresh.CaptureId, Ct))!.Run.Status);
            Assert.IsEmpty((await fixture.Store.GetAsync(first.CaptureId, cancellationToken: Ct))!.Results);
        }
        finally
        {
            late.TrySetResult(fixture.Preferred.Success());
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
    }

    [TestMethod]
    public async Task ShutdownWhileProviderIsUnavailablePreservesBacklogForRestart()
    {
        using var fixture = new Fixture(TimeSpan.FromMilliseconds(40));
        var late = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        AnalysisRequest first = await fixture.EnqueueAsync(Ct);
        AnalysisRequest second = await fixture.EnqueueAsync(Ct);
        fixture.Preferred.Execute = (input, _) => input.CaptureId == first.CaptureId ? late.Task : Task.FromResult(fixture.Preferred.Success());
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.IsFalse(late.Task.IsCompleted);
            Assert.AreEqual(AnalysisRunStatus.Queued, (await fixture.Store.GetWorkAsync(second.CaptureId, Ct))!.Run.Status);
        }
        finally
        {
            late.TrySetResult(fixture.Preferred.Success());
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
        using LocalCaptureAnalysisStore reopened = fixture.Environment.CreateStore();
        using var restartedWorker = new CaptureAnalysisWorker(reopened, fixture.Authorization, fixture.Source, fixture.Configuration, fixture.Adapters, fixture.Catalog);
        await DrainAsync(restartedWorker, Ct);
        AnalysisWorkItem completed = (await reopened.GetWorkAsync(second.CaptureId, Ct))!;
        Assert.AreEqual(second.RequestId, completed.Run.Id);
        Assert.AreEqual(AnalysisRunStatus.Completed, completed.Run.Status);
        CollectionAssert.AreEqual(new[] { "preferred", "preferred", "description" }, fixture.Calls.ToArray());
    }

    [TestMethod]
    public async Task RevocationAndReenableCannotAuthorizeOldQueuedWork()
    {
        using var fixture = new Fixture();
        AnalysisRequest old = await fixture.EnqueueAsync(Ct);
        await fixture.Authorization.ChangeAsync(false, Ct);
        AnalysisAdmissionScope scope = await fixture.Store.GetAdmissionScopeAsync(Ct);
        Assert.IsFalse(await fixture.Worker.EnqueueAsync(AnalysisExecutionStoreTests.Request(fixture.Environment, scope)
            with { ExpectedAuthorizationId = old.ExpectedAuthorizationId }, Ct));
        await fixture.Authorization.ChangeAsync(true, Ct);
        AnalysisRequest fresh = await fixture.EnqueueAsync(Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(AnalysisRunStatus.Cancelled, (await fixture.Store.GetWorkAsync(old.CaptureId, Ct))!.Run.Status);
        Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(fresh.CaptureId, Ct))!.Run.Status);
        Assert.HasCount(2, fixture.Calls);
    }

    [TestMethod]
    public async Task ClearWhileInferenceRunsRejectsLatePublicationAndStaleAdmission()
    {
        using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Preferred.Execute = (_, _) => { entered.TrySetResult(); return release.Task; };
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            await fixture.Worker.ClearAsync(123, Ct);
            Assert.IsFalse(await fixture.Worker.EnqueueAsync(request, Ct));
            release.SetResult(fixture.Preferred.Success());
            Assert.IsEmpty(await fixture.Store.ReadAllAsync(Ct));
            Assert.IsEmpty(await fixture.Store.ReadPendingAsync(Ct));
        }
        finally
        {
            release.TrySetResult(fixture.Preferred.Success());
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
        Assert.IsEmpty(await fixture.Store.ReadAllAsync(Ct));
    }

    [TestMethod]
    public async Task ShutdownBeforeCommitResumesTheSameUnfinishedRun()
    {
        using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Preferred.Execute = async (_, ct) => { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); return fixture.Preferred.Success(); };
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        shutdown.Cancel();
        await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        AnalysisWorkItem interrupted = (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!;
        Assert.IsTrue(interrupted.Run.IsPending);
        Assert.IsEmpty(interrupted.Run.CompletedSteps);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(fixture.Preferred.Success());
        await DrainAsync(fixture.Worker, Ct);
        Assert.AreEqual(request.RequestId, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Id);
        CollectionAssert.AreEqual(new[] { "preferred", "preferred", "description" }, fixture.Calls.ToArray());
    }

    [TestMethod]
    public async Task RevocationDuringInferenceFencesPublicationAfterReenable()
    {
        using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Preferred.Execute = (_, _) => { entered.TrySetResult(); return release.Task; };
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            await fixture.Authorization.ChangeAsync(false, Ct);
            await fixture.Authorization.ChangeAsync(true, Ct);
            release.SetResult(fixture.Preferred.Success());
            while ((await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.IsPending) await Task.Delay(10, Ct);
            Assert.IsEmpty((await fixture.Store.GetAsync(request.CaptureId, cancellationToken: Ct))!.Results);
            Assert.AreEqual(AnalysisRunStatus.Cancelled, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
        }
        finally { release.TrySetResult(fixture.Preferred.Success()); shutdown.Cancel(); await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct); }
    }

    [TestMethod]
    public async Task ClearIsDurableEvenWhenAProviderCancellationCallbackThrows()
    {
        using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Preferred.Execute = async (_, ct) =>
        {
            using var callback = ct.Register(() => throw new InvalidOperationException("Broken provider callback."));
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return fixture.Preferred.Success();
        };
        await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.IsTrue((await fixture.Worker.ClearAsync(42, Ct)).Completed);
            Assert.IsEmpty(await fixture.Store.ReadPendingAsync(Ct));
        }
        finally { shutdown.Cancel(); await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct); }
        Assert.IsEmpty(await fixture.Store.ReadAllAsync(Ct));
    }

    [TestMethod]
    public async Task ChangedSourceOnRestartNeverReusesCommittedCompletionMarkers()
    {
        using var fixture = new Fixture();
        AnalysisRequest request = await fixture.EnqueueAsync(Ct);
        AnalysisWorkItem work = (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!;
        await fixture.Store.BindSourceAsync(work.Token, AnalysisTestEnvironment.Revision('b'), Ct);
        await DrainAsync(fixture.Worker, Ct);
        Assert.IsEmpty(fixture.Calls);
        Assert.AreEqual(AnalysisRunStatus.InvalidSource, (await fixture.Store.GetWorkAsync(request.CaptureId, Ct))!.Run.Status);
    }

    [TestMethod]
    public async Task UnadmittedRequestsCannotInheritAuthorizationAfterDisableAndReenable()
    {
        using var fixture = new Fixture();
        AnalysisRequest request = AnalysisExecutionStoreTests.Request(fixture.Environment, await fixture.Store.GetAdmissionScopeAsync(Ct));
        using (var grant = await fixture.Authorization.AcquireAsync(Ct))
            request = request with { ExpectedAuthorizationId = grant.Revision };
        await fixture.Authorization.ChangeAsync(false, Ct);
        await fixture.Authorization.ChangeAsync(true, Ct);
        Assert.IsFalse(await fixture.Worker.EnqueueAsync(request, Ct));
        Assert.IsFalse(await fixture.Worker.EnqueueAsync(request with { ExpectedAuthorizationId = null }, Ct));
        Assert.IsNull(await fixture.Store.GetWorkAsync(request.CaptureId, Ct));
        using (var grant = await fixture.Authorization.AcquireAsync(Ct))
            request = request with { ExpectedAuthorizationId = grant.Revision };
        Assert.IsTrue(await fixture.Worker.EnqueueAsync(request, Ct));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ProvidersReceiveTheCatalogCaptureDateOrAnExplicitUnknown(bool registered)
    {
        using var fixture = new Fixture();
        var request = await fixture.EnqueueAsync(Ct);
        DateTimeOffset capturedAt = new(2024, 3, 4, 5, 6, 7, TimeSpan.FromHours(-7));
        if (registered)
            await fixture.Catalog.RegisterAsync(new(request.CaptureId, CaptureFileType.Image, capturedAt,
                request.SourcePath, CaptureSourceOwnership.Application), Ct);
        AnalysisInput? received = null;
        fixture.Preferred.Execute = (input, _) => { received = input; return Task.FromResult(fixture.Preferred.Success()); };
        await DrainAsync(fixture.Worker, Ct);
        Assert.IsNotNull(received);
        Assert.AreEqual(registered ? capturedAt : (DateTimeOffset?)null, received.CapturedAt);
        Assert.AreEqual(request.CaptureId, received.CaptureId);
    }

    private static async Task WaitForSnapshotAsync(CaptureAnalysisWorker worker, Func<AnalysisActivitySnapshot, bool> predicate, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (!predicate(worker.Progress)) await Task.Delay(10, deadline.Token);
    }

    private static async Task DrainAsync(CaptureAnalysisWorker worker, CancellationToken ct)
    {
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(AnalysisActivitySnapshot progress) { if (progress.Activity == AnalysisActivity.Idle) idle.TrySetResult(); }
        worker.ProgressChanged += Observe;
        Task runner = worker.RunAsync(shutdown.Token);
        try { await idle.Task.WaitAsync(TimeSpan.FromSeconds(10), ct); }
        finally
        {
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), ct);
            worker.ProgressChanged -= Observe;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public AnalysisTestEnvironment Environment { get; } = new();
        public LocalCaptureAnalysisStore Store { get; }
        public LocalCaptureAssetCatalog Catalog { get; }
        public TestAuthorization Authorization { get; } = new();
        public TestSource Source { get; } = new();
        public ConcurrentQueue<string> Calls { get; } = new();
        public FakeAnalyzer Preferred { get; }
        public FakeAnalyzer Fallback { get; }
        public FakeAnalyzer Description { get; }
        public IMediaAnalyzer[] Adapters => [Preferred, Fallback, Description];
        public CaptureAnalysisConfiguration Configuration { get; }
        public CaptureAnalysisWorker Worker { get; }
        public Fixture(TimeSpan? timeout = null)
        {
            Store = Environment.CreateStore();
            Catalog = Environment.CreateCatalog();
            Preferred = new("preferred", AnalysisCapability.TextRecognition, Calls);
            Fallback = new("fallback", AnalysisCapability.TextRecognition, Calls);
            Description = new("description", AnalysisCapability.Description, Calls);
            Configuration = new([new(AnalysisMediaKind.Image, "v1", [
                new(AnalysisCapability.TextRecognition, ["preferred", "fallback"], TimeSpan.FromSeconds(1), timeout ?? TimeSpan.FromSeconds(5)),
                new(AnalysisCapability.Description, ["description"], TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)),
            ])]);
            Worker = new(Store, Authorization, Source, Configuration, Adapters, Catalog);
        }
        public async Task<AnalysisRequest> EnqueueAsync(CancellationToken ct, string? language = null)
        {
            AnalysisRequest request = AnalysisExecutionStoreTests.Request(Environment, await Store.GetAdmissionScopeAsync(ct));
            request = request with { SourcePath = Path.Combine(Environment.Root, request.CaptureId + ".png"), Language = language };
            using (IAnalysisAuthorizationLease grant = await Authorization.AcquireAsync(ct))
                request = request with { ExpectedAuthorizationId = grant.Revision };
            Assert.IsTrue(await Worker.EnqueueAsync(request, ct));
            return request;
        }
        public void Dispose() { Worker.Dispose(); Store.Dispose(); Catalog.Dispose(); Authorization.Dispose(); Environment.Dispose(); }
    }

    private sealed class FakeAnalyzer : IMediaAnalyzer
    {
        private readonly ConcurrentQueue<string> _calls;
        public MediaAnalyzerDescriptor Descriptor { get; }
        public AnalyzerAvailability Ready { get; set; } = AnalyzerAvailability.Ready;
        public string? UnsupportedLanguage { get; set; }
        public IProgress<AnalysisProgress>? Progress { get; private set; }
        public Func<AnalysisInput, CancellationToken, Task<AnalyzerOutcome>> Execute { get; set; }
        public FakeAnalyzer(string id, AnalysisCapability capability, ConcurrentQueue<string> calls)
        {
            _calls = calls;
            Descriptor = new(id, capability, [AnalysisMediaKind.Image]);
            Execute = (_, _) => Task.FromResult(Success());
        }
        public AnalyzerOutcome Success() => AnalyzerOutcome.Success(Descriptor.Capability == AnalysisCapability.TextRecognition
            ? new TextRecognitionMetadata([]) : new DescriptionMetadata([new("description")]), new(Descriptor.Id, "fake", "model", "1"));
        public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct) =>
            ValueTask.FromResult(language != null && language == UnsupportedLanguage ? AnalyzerAvailability.Unsupported : Ready);
        public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            _calls.Enqueue("prepare:" + Descriptor.Id);
            Ready = AnalyzerAvailability.Ready;
            progress?.Report(new(AnalysisProgressStage.Preparing, 1));
            return Task.FromResult(Ready);
        }
        public Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
        {
            _calls.Enqueue(Descriptor.Id);
            Progress = progress;
            return Execute(input, ct);
        }
    }

    private sealed class TestSource : IAnalysisSource
    {
        public bool Unchanged { get; set; } = true;
        public string? FailPath { get; set; }
        public int Disposals { get; private set; }
        public Task<IAnalysisSourceLease> OpenAsync(string path, CancellationToken ct) => path == FailPath
            ? throw new FileNotFoundException() : Task.FromResult<IAnalysisSourceLease>(new Lease(this, path));
        private sealed class Lease(TestSource source, string path) : IAnalysisSourceLease
        {
            public string Path => path;
            public SourceRevision Revision => AnalysisTestEnvironment.Revision();
            public Task<bool> VerifyAsync(CancellationToken ct) => Task.FromResult(source.Unchanged);
            public ValueTask DisposeAsync() { source.Disposals++; return ValueTask.CompletedTask; }
        }
    }

    private sealed class TestAuthorization : IAnalysisAuthorization, IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly List<CancellationTokenSource> _revocations = [];
        private CancellationTokenSource _revoked = new();
        private Guid _revision = Guid.NewGuid();
        private bool _allowed = true;
        public async ValueTask<IAnalysisAuthorizationLease> AcquireAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            return new Lease(_allowed, _revision, _revoked.Token, _gate);
        }
        public async Task ChangeAsync(bool allowed, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                _revoked.Cancel();
                _revocations.Add(_revoked);
                _revoked = new();
                _revision = Guid.NewGuid();
                _allowed = allowed;
            }
            finally { _gate.Release(); }
        }
        public void Dispose() { _revoked.Dispose(); foreach (var item in _revocations) item.Dispose(); _gate.Dispose(); }
        private sealed class Lease(bool allowed, Guid revision, CancellationToken revoked, SemaphoreSlim gate) : IAnalysisAuthorizationLease
        {
            public bool IsAllowed => allowed;
            public Guid Revision => revision;
            public CancellationToken Revoked => revoked;
            public void Dispose() => gate.Release();
        }
    }
}
