namespace CaptureTool.Infrastructure.Capture.Windows.V2;

public sealed class CaptureCallbackRegistration : IDisposable, IAsyncDisposable
{
    private readonly Action<CaptureCallbackRegistration> _onDisposed;
    private CaptureCallbackRegistrationSafeHandle? _handle;
    private int _disposed;

    internal CaptureCallbackRegistration(
        CaptureCallbackRegistrationSafeHandle handle,
        Action<CaptureCallbackRegistration> onDisposed)
    {
        _handle = handle;
        _onDisposed = onDisposed;
    }

    public void Dispose()
    {
        DisposeCore(notifyOwner: true);
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    internal void DisposeFromOwner()
        => DisposeCore(notifyOwner: false);

    private void DisposeCore(bool notifyOwner)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _handle?.Dispose();
        _handle = null;

        if (notifyOwner)
        {
            _onDisposed(this);
        }
    }
}
