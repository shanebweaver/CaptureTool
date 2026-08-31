namespace CaptureTool.Presentation.Features.CaptureMemory;

/// <summary>
/// Reconciles visible Capture Memory views with durable state, including changes made by
/// background intake and other pages. Owns no policy or work; leaving the page stops reads.
/// Start/Dispose and refresh callbacks run on the owning view's synchronization context.
/// </summary>
internal sealed class CaptureMemoryStateRefreshLoop(
    Func<CancellationToken, Task> refresh,
    Func<CancellationToken, Task>? waitForNextRefresh = null) : IDisposable
{
    private readonly Func<CancellationToken, Task> _waitForNextRefresh = waitForNextRefresh ??
        (token => Task.Delay(TimeSpan.FromSeconds(1), token));
    private CancellationTokenSource? _lifetime;

    internal Task Completion { get; private set; } = Task.CompletedTask;

    public void Start()
    {
        if (_lifetime != null)
        {
            return;
        }

        _lifetime = new CancellationTokenSource();
        Completion = RunAsync(_lifetime);
    }

    public void Dispose()
    {
        CancellationTokenSource? lifetime = _lifetime;
        _lifetime = null;
        lifetime?.Cancel();
    }

    private async Task RunAsync(CancellationTokenSource lifetime)
    {
        try
        {
            while (true)
            {
                await _waitForNextRefresh(lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                try
                {
                    await refresh(lifetime.Token);
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // A transient status read failure must not permanently stop reconciliation.
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_lifetime, lifetime))
            {
                _lifetime = null;
            }
            lifetime.Dispose();
        }
    }
}
