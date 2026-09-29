using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using System.Diagnostics;

// Cached model weights only: checks native load/reuse/release and the Windows
// memory signal. Worker tests separately verify the idle deadline and cancellation.
internal static class ModelReuseChecks
{
    internal static async Task<int> RunAsync(IStorageService storage)
    {
        using var runtime = new FoundryRuntime(storage);
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var model = await runtime.ResolveAsync("phi-4-mini", budget.Token)
            ?? throw new InvalidOperationException("No CPU model.");
        if (!await model.IsCachedAsync(budget.Token))
            throw new InvalidOperationException("This diagnostic requires cached Phi weights; it never downloads models.");
        try
        {
            foreach (string stage in new[] { "cold", "warm", "after-release" })
            {
                var timer = Stopwatch.StartNew();
                await using (await runtime.AcquireModelAsync(model, budget.Token, retainWhenIdle: true)) { }
                Console.WriteLine($"{stage}: acquisition {timer.Elapsed.TotalSeconds:F3}s; idle retention {runtime.IdleRetention.TotalSeconds:F0}s; low memory {runtime.IsUnderMemoryPressure}");
                if (runtime.IdleRetention != TimeSpan.FromSeconds(30)) return 1;
                if (stage == "cold") await Task.Delay(TimeSpan.FromSeconds(2), budget.Token);
                if (stage == "warm")
                {
                    await runtime.ReleaseAsync();
                    if (runtime.IdleRetention != TimeSpan.Zero) return 1;
                }
            }
        }
        finally { await runtime.ReleaseAsync(); }
        Console.WriteLine("Native model reuse and release checks passed. No inference or capture access.");
        return runtime.IdleRetention == TimeSpan.Zero ? 0 : 1;
    }
}
