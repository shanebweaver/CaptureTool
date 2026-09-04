using CaptureTool.Presentation.Features.CaptureMemory;
using System.Threading.Channels;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureMemoryStateRefreshLoopTests
{
    [TestMethod]
    public async Task RefreshLoop_ShouldRecoverFromReadFailureAndStopAndRestartWithoutDuplicateLoops()
    {
        var ticks = Channel.CreateUnbounded<bool>();
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        using var loop = new CaptureMemoryStateRefreshLoop(_ =>
        {
            int read = Interlocked.Increment(ref reads);
            if (read == 1)
            {
                throw new IOException("Transient status failure");
            }
            refreshed.TrySetResult();
            return Task.CompletedTask;
        }, async token => { await ticks.Reader.ReadAsync(token); });

        loop.Start();
        Task firstRun = loop.Completion;
        loop.Start();
        Assert.AreSame(firstRun, loop.Completion);
        ticks.Writer.TryWrite(true);
        ticks.Writer.TryWrite(true);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        loop.Dispose();
        await firstRun.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(2, reads);

        refreshed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.Start();
        ticks.Writer.TryWrite(true);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        loop.Dispose();
        loop.Dispose();
        await loop.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(3, reads);
    }
}
