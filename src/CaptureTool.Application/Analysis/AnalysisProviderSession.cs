using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Logging;

namespace CaptureTool.Application.Analysis;

/// <summary>
/// Owns native calls, model cleanup, and source leases until native work actually stops.
/// The worker uses this from its single execution loop; cancellation commands do not enter it.
/// </summary>
internal sealed class AnalysisProviderSession
{
    private static readonly TimeSpan CancellationGrace = TimeSpan.FromSeconds(2);
    private readonly IAnalysisResources[] _resources;
    private readonly ILogService? _log;
    private Task? _invocation;

    public AnalysisProviderSession(IEnumerable<IAnalysisResources> resources, ILogService? log)
    {
        _resources = resources.ToArray();
        _log = log;
    }

    public Task? PendingInvocation => _invocation is { IsCompleted: false } ? _invocation : null;
    public TimeSpan IdleRetention => _resources.Length == 0
        ? TimeSpan.Zero
        : _resources.Min(resource => resource.IdleRetention);
    public bool IsUnderMemoryPressure => _resources.Any(resource => resource.IsUnderMemoryPressure);

    public async Task<T> InvokeAsync<T>(Func<CancellationToken, Task<T>> invoke, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (PendingInvocation != null)
        {
            throw new AnalysisProviderBusyException();
        }

        var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<T> task = Task.Run(async () =>
        {
            try
            {
                return await invoke(attempt.Token).ConfigureAwait(false);
            }
            finally
            {
                attempt.Dispose();
            }
        }, CancellationToken.None);
        _invocation = task;

        try
        {
            return await task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            RequestCancellation(attempt);
            await WaitForCancellationAsync(task).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task WaitForCancellationAsync(Task invocation)
    {
        try
        {
            await invocation.WaitAsync(CancellationGrace, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _ = ObserveAsync(invocation);
            throw new AnalysisProviderBusyException();
        }
        catch (Exception) { /* Observe completion before allowing another provider. */ }
    }

    public ValueTask ReleaseSourceAsync(IAnalysisSourceLease source)
    {
        if (PendingInvocation is not { } invocation)
        {
            return source.DisposeAsync();
        }

        // A logical timeout fences publication, not native ownership. Include source
        // disposal in the same gate that protects the next invocation and cleanup.
        _invocation = ReleaseSourceAfterInvocationAsync(invocation, source);
        _ = ObserveAsync(_invocation);
        return ValueTask.CompletedTask;
    }

    private static async Task ReleaseSourceAfterInvocationAsync(Task invocation, IAnalysisSourceLease source)
    {
        await ObserveAsync(invocation).ConfigureAwait(false);
        await source.DisposeAsync().ConfigureAwait(false);
    }

    public Task<bool> ReleaseResourcesAsync()
    {
        if (_resources.Length == 0)
        {
            return Task.FromResult(true);
        }

        Task? previous = _invocation;
        Task<bool> cleanup = Task.Run(() => ReleaseResourcesAfterInvocationAsync(previous));
        _invocation = cleanup;
        return cleanup;
    }

    private async Task<bool> ReleaseResourcesAfterInvocationAsync(Task? previous)
    {
        if (previous != null)
        {
            await ObserveAsync(previous).ConfigureAwait(false);
        }

        bool released = true;
        foreach (IAnalysisResources resource in _resources)
        {
            try
            {
                await resource.ReleaseAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                released = false;
                _log?.LogWarning("Analysis provider resource release failed.");
            }
        }
        return released;
    }

    public async Task ShutdownAsync()
    {
        bool invocationStopped = PendingInvocation == null;
        Task cleanup = ReleaseResourcesAsync();
        if (!invocationStopped)
        {
            // Cleanup follows native completion without blocking shutdown.
            _ = ObserveAsync(cleanup);
            return;
        }

        try
        {
            // Unloading can itself hang. Retain ownership but bound the shutdown wait.
            await ObserveAsync(cleanup).WaitAsync(CancellationGrace).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _log?.LogWarning("Analysis resource cleanup continues after shutdown grace expired.");
        }
    }

    internal static void RequestCancellation(CancellationTokenSource? source)
    {
        // Third-party callbacks must not block or fail a clear/cancel command.
        try
        {
            if (source != null)
            {
                _ = ObserveAsync(source.CancelAsync());
            }
        }
        catch (ObjectDisposedException) { }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}

internal sealed class AnalysisProviderBusyException : Exception;
