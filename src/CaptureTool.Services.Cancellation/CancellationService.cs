using System;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Services.Cancellation;

public sealed partial class CancellationService : ICancellationService, IDisposable
{
    private readonly CancellationTokenSource _rootTokenSource;
    private bool disposed;

    public CancellationService()
    {
        _rootTokenSource = new();
    }

    public CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken? cancellationToken = null)
    {
        if (cancellationToken is not null)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(_rootTokenSource.Token, cancellationToken.Value);
        }

        return CancellationTokenSource.CreateLinkedTokenSource(_rootTokenSource.Token);
    }

    public void CancelAll()
    {
        _rootTokenSource.Cancel();
    }

    public async Task CancelAllAsync()
    {
        await _rootTokenSource.CancelAsync();
    }

    private void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                _rootTokenSource.Dispose();
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
