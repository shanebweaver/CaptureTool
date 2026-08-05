namespace CaptureTool.Presentation.Features.SelectionOverlay;

internal sealed class SelectionOverlayHostLifetime : IDisposable
{
    private readonly Action _closeCore;
    private int _closed;
    private int _disposed;

    public SelectionOverlayHostLifetime(Action closeCore)
    {
        ArgumentNullException.ThrowIfNull(closeCore);
        _closeCore = closeCore;
    }

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        _closeCore();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Close();
    }
}
