using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Infrastructure.Tests.Analysis;

public sealed partial class CaptureAnalysisWorkerTests
{
    [TestMethod]
    public async Task IndependentRequestsReuseResourcesAndRestartTheThirtySecondIdleWindow()
    {
        var time = new IdleClock();
        var resources = new TestResources { IdleRetention = TimeSpan.FromSeconds(30) };
        var name = new ScreenshotProcessor(AnalysisCapability.CaptureName);
        var summary = new ScreenshotProcessor(AnalysisCapability.CaptureSynopsis);
        var alt = new ScreenshotProcessor(AnalysisCapability.ImageAltText);
        using var fixture = new Fixture(processors: [name, summary, alt], resources: [resources], time: time);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        var first = await fixture.EnqueueAsync(Ct, capabilities: ImageAction(fixture, AnalysisCapability.CaptureName));
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForIdleConditionAsync(() => name.Calls == 1 && fixture.Worker.Progress.Activity == AnalysisActivity.Idle);
            Assert.AreEqual(0, summary.Calls);
            Assert.AreEqual(0, alt.Calls);
            Assert.AreEqual(0, resources.Releases);
            Assert.IsEmpty(await fixture.Store.ReadPendingAsync(Ct), "Warm resources must not make the UI or queue busy.");
            time.Advance(TimeSpan.FromSeconds(20));
            fixture.Calls.Clear();
            Assert.IsTrue(await fixture.Worker.EnqueueAsync(first with
            {
                RequestId = Guid.NewGuid(), ExpectedRunId = first.RequestId, ReuseExisting = true,
                Capabilities = ImageAction(fixture, AnalysisCapability.CaptureSynopsis),
            }, Ct));
            await WaitForIdleConditionAsync(() => summary.Calls == 1 && fixture.Worker.Progress.Activity == AnalysisActivity.Idle);
            Assert.IsEmpty(fixture.Calls, "OCR and description must be reused for the next feature.");
            Assert.AreEqual(0, resources.Releases, "A nearby request must run before model release.");
            time.Advance(TimeSpan.FromSeconds(29));
            // Real timers only wake the worker; the injected monotonic clock controls its deadline.
            await Task.Delay(TimeSpan.FromMilliseconds(1100), Ct);
            Assert.AreEqual(0, resources.Releases, "The new request resets the original deadline.");
            time.Advance(TimeSpan.FromSeconds(1));
            await WaitForIdleConditionAsync(() => resources.Releases != 0);
            Assert.AreEqual(1, resources.Releases);
            Assert.AreEqual(1, name.Calls);
            Assert.AreEqual(1, summary.Calls);
            Assert.AreEqual(0, alt.Calls);
        }
        finally { shutdown.Cancel(); await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct); }
    }

    [TestMethod]
    [DataRow("shutdown")]
    [DataRow("pressure")]
    [DataRow("consent")]
    [DataRow("delete")]
    [DataRow("cancel")]
    public async Task IdleRetentionEndsEarlyWhenResourcesShouldBeReleased(string reason)
    {
        var resources = new TestResources { IdleRetention = TimeSpan.FromSeconds(30) };
        using var fixture = new Fixture(resources: [resources], time: new IdleClock());
        var request = await fixture.EnqueueAsync(Ct);
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForIdleConditionAsync(() => fixture.Worker.Progress.Activity == AnalysisActivity.Idle && fixture.Worker.Progress.CaptureId == request.CaptureId);
            Assert.AreEqual(0, resources.Releases);
            switch (reason)
            {
                case "shutdown": shutdown.Cancel(); await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct); break;
                case "pressure": resources.IsUnderMemoryPressure = true; break;
                case "consent": await fixture.Authorization.ChangeAsync(false, Ct); break;
                case "delete": await fixture.Worker.ClearAsync(12, Ct); break;
                case "cancel": await fixture.Worker.CancelAsync(request.CaptureId, Ct); break;
            }
            await WaitForIdleConditionAsync(() => resources.Releases != 0);
        }
        finally { shutdown.Cancel(); await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct); }
    }

    [TestMethod]
    public async Task MemoryPressureDoesNotReleaseAnActiveInvocation()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var name = new ScreenshotProcessor(AnalysisCapability.CaptureName) { WaitForCompletion = completion.Task };
        var resources = new TestResources { IdleRetention = TimeSpan.FromSeconds(30) };
        using var fixture = new Fixture(processors: [name], resources: [resources]);
        fixture.Preferred.Execute = (_, _) => Task.FromResult(SourceSuccess(fixture));
        await fixture.EnqueueAsync(Ct, capabilities: ImageAction(fixture, AnalysisCapability.CaptureName));
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task runner = fixture.Worker.RunAsync(shutdown.Token);
        try
        {
            await WaitForIdleConditionAsync(() => name.Calls == 1);
            resources.IsUnderMemoryPressure = true;
            await Task.Delay(100, Ct);
            Assert.AreEqual(0, resources.Releases);
            completion.SetResult();
            await WaitForIdleConditionAsync(() => resources.Releases != 0);
        }
        finally { completion.TrySetResult(); shutdown.Cancel(); await runner.WaitAsync(TimeSpan.FromSeconds(5), Ct); }
    }

    private async Task WaitForIdleConditionAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }

    private sealed class IdleClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _ticks);
        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);
    }
}
