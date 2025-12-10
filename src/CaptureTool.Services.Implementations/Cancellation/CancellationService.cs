using CaptureTool.Services.Interfaces.Cancellation;

namespace CaptureTool.Services.Implementations.Cancellation;

public sealed partial class CancellationService : ICancellationService, IDisposable
{
    private CancellationTokenSource _rootTokenSource;

    public event EventHandler? CancelAllRequested;

    public CancellationService()
    {
        _rootTokenSource = new();
    }

    public CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken? cancellationToken = null)
    {
        if (_rootTokenSource.IsCancellationRequested)
        {
            Reset();
        }

        if (cancellationToken is not null)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(_rootTokenSource.Token, cancellationToken.Value);
        }

        return CancellationTokenSource.CreateLinkedTokenSource(_rootTokenSource.Token);
    }

    public void Reset()
    {
        if (!_rootTokenSource.TryReset())
        {
            _rootTokenSource.Dispose();
            _rootTokenSource = new();
        }
    }

    public void CancelAll()
    {
        _rootTokenSource.Cancel();
        CancelAllRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task CancelAllAsync()
    {
        await _rootTokenSource.CancelAsync();
        CancelAllRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _rootTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
