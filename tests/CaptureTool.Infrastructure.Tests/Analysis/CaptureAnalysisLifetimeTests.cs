using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Infrastructure.Analysis.Sources;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureAnalysisWorkerTests
{
    [TestMethod]
    public async Task ShutdownIsBoundedAndRestartWaitsForUnresponsiveModelUnload()
    {
        var resources = new HangingResources();
        using var fixture = new Fixture(resources: [resources]);
        var request = await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var restartShutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        Task? restarted = null;
        try
        {
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle && value.CaptureId == request.CaptureId, Ct);
            shutdown.Cancel();
            await resources.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            Assert.IsFalse(resources.Release.Task.IsCompleted);

            fixture.Calls.Clear();
            await fixture.EnqueueAsync(Ct);
            restarted = fixture.Worker.RunAsync(restartShutdown.Token);
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            Assert.IsEmpty(fixture.Calls, "A restarted worker must preserve ownership held by cleanup.");
            resources.Release.SetResult();
            await WaitForSnapshotAsync(fixture.Worker, value => value.Activity == AnalysisActivity.Idle, Ct);
            Assert.IsNotEmpty(fixture.Calls);
        }
        finally
        {
            resources.Release.TrySetResult();
            shutdown.Cancel();
            restartShutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            if (restarted != null) await restarted.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
    }

    [TestMethod]
    [DataRow("timeout", false)]
    [DataRow("timeout", true)]
    [DataRow("cancel", false)]
    [DataRow("shutdown", false)]
    public async Task SourceRemainsProtectedUntilActualProviderCompletion(string stop, bool fault)
    {
        using var fixture = new Fixture(stop == "timeout" ? TimeSpan.FromMilliseconds(40) : TimeSpan.FromMinutes(1));
        var first = await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.TextRecognition]);
        var next = await fixture.EnqueueAsync(Ct, capabilities: [AnalysisCapability.TextRecognition]);
        await File.WriteAllTextAsync(first.SourcePath, "selected", Ct);
        await File.WriteAllTextAsync(next.SourcePath, "next", Ct);
        var resources = new TestResources();
        using var worker = new CaptureAnalysisWorker(fixture.Store, fixture.Authorization, new LocalAnalysisSource(),
            fixture.Configuration, fixture.Adapters, fixture.Catalog, resources: [resources]);
        first = await EnqueueRealSourceAsync(worker, first);
        next = await EnqueueRealSourceAsync(worker, next);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<AnalyzerOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Preferred.Execute = (input, _) =>
        {
            if (input.CaptureId != first.CaptureId) return Task.FromResult(fixture.Preferred.Success());
            entered.TrySetResult();
            return late.Task;
        };
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = worker.RunAsync(shutdown.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            if (stop == "cancel") await worker.CancelAsync(first.CaptureId, Ct);
            if (stop == "shutdown")
            {
                shutdown.Cancel();
                await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            }
            else
            {
                await WaitForSnapshotAsync(worker, value => value.Activity == AnalysisActivity.ProviderUnavailable, Ct);
            }
            Assert.AreEqual(0, resources.Releases, "A live invocation still owns its model.");
            Assert.HasCount(1, fixture.Calls);
            Assert.Throws<IOException>(() => File.Move(first.SourcePath, first.SourcePath + ".moved"));
            Assert.Throws<IOException>(() => { using var stream = File.Open(first.SourcePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });

            if (fault) late.SetException(new InvalidOperationException("Late provider failure"));
            else late.SetResult(fixture.Preferred.Success());
            await WaitForIdleConditionAsync(() => resources.Releases > 0);
            File.Move(first.SourcePath, first.SourcePath + ".moved");
            Assert.IsEmpty((await fixture.Store.GetAsync(first.CaptureId, cancellationToken: Ct))?.Results ?? []);
            if (stop != "shutdown")
            {
                Assert.AreEqual(AnalysisRunStatus.Completed, (await fixture.Store.GetWorkAsync(next.CaptureId, Ct))!.Run.Status);
            }
        }
        finally
        {
            late.TrySetResult(fixture.Preferred.Success());
            shutdown.Cancel();
            await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct);
            await WaitForIdleConditionAsync(() => resources.Releases > 0);
        }
    }

    private sealed class HangingResources : IAnalysisResources
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TimeSpan IdleRetention => TimeSpan.FromMinutes(1);
        public bool IsUnderMemoryPressure => false;
        public Task ReleaseAsync() { Entered.TrySetResult(); return Release.Task; }
    }
}
