using CaptureTool.Services.Cancellation;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockCancellationService : ICancellationService
{
    public void CancelAll()
    {
    }

    public Task CancelAllAsync()
    {
        return Task.CompletedTask;
    }

    public CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken? cancellationToken = null)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken ?? CancellationToken.None);
    }

    public void Reset()
    {
    }
}
